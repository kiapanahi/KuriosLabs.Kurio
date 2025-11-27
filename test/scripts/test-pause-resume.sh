#!/usr/bin/env bash

#######################################
# Kurio Download Manager - Pause/Resume Test Script
#
# This script tests the pause and resume functionality by:
# 1. Creating a download task with configurable parameters
# 2. Waiting for a specified duration
# 3. Pausing the download
# 4. Reading the bytes downloaded from the state file
# 5. Prompting the user to resume
# 6. Resuming the download
#
# Usage: ./test-pause-resume.sh
#######################################

set -euo pipefail

#######################################
# Configuration Variables
#######################################

# Server Configuration
API_BASE_URL="${API_BASE_URL:-http://localhost:5205}"
API_ENDPOINT="${API_BASE_URL}/api/downloads"

# Download Configuration
DOWNLOAD_URL="${DOWNLOAD_URL:-https://dl2.soft98.ir/soft/g/Google.Chrome.142.0.7444.176.x64.zip?1764238169}"
DOWNLOAD_FILENAME=""
DESTINATION_DIR="${DESTINATION_DIR:-/tmp/kurio-test}"
MAX_CONNECTIONS="${MAX_CONNECTIONS:-8}"
PRIORITY="${PRIORITY:-Normal}"

# Test Configuration
WAIT_SECONDS="${WAIT_SECONDS:-3}"

# State Configuration
STATE_DIR="${STATE_DIR:-$HOME/.kurio/state}"

#######################################
# Color Output
#######################################

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

#######################################
# Helper Functions
#######################################

log_info() {
    echo -e "${CYAN}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "\n${BLUE}==>${NC} ${BLUE}$1${NC}\n"
}

check_dependencies() {
    local missing=0
    
    if ! command -v curl &> /dev/null; then
        log_error "curl is not installed"
        missing=1
    fi
    
    if ! command -v jq &> /dev/null; then
        log_error "jq is not installed (brew install jq)"
        missing=1
    fi
    
    if [ $missing -eq 1 ]; then
        exit 1
    fi
}

check_server() {
    log_info "Checking if Kurio server is running..."
    
    if ! curl -s -f "${API_BASE_URL}/health" &> /dev/null; then
        log_error "Server is not responding at ${API_BASE_URL}"
        log_error "Please start the server: dotnet run --project src/Kurio.Server"
        exit 1
    fi
    
    log_success "Server is running at ${API_BASE_URL}"
}

#######################################
# Main Script
#######################################

main() {
    log_step "Kurio Download Manager - Pause/Resume Test"
    
    # Pre-flight checks
    check_dependencies
    check_server
    
    # Display configuration
    log_info "Configuration:"
    echo "  API URL:         ${API_ENDPOINT}"
    echo "  Download URL:    ${DOWNLOAD_URL}"
    echo "  Filename:        ${DOWNLOAD_FILENAME}"
    echo "  Destination:     ${DESTINATION_DIR}"
    echo "  Connections:     ${MAX_CONNECTIONS}"
    echo "  Priority:        ${PRIORITY}"
    echo "  Wait Duration:   ${WAIT_SECONDS}s"
    echo "  State Directory: ${STATE_DIR}"
    echo ""
    
    # Step 1: Create download task
    log_step "Step 1: Creating download task"
    
    local request_payload
    request_payload=$(cat <<EOF
{
    "url": "${DOWNLOAD_URL}",
    "fileName": "${DOWNLOAD_FILENAME}",
    "destinationDirectory": "${DESTINATION_DIR}",
    "maxConnections": ${MAX_CONNECTIONS},
    "priority": "${PRIORITY}"
}
EOF
)
    
    log_info "Sending request to create download..."
    
    local response
    response=$(curl -s -X POST "${API_ENDPOINT}" \
        -H "Content-Type: application/json" \
        -d "${request_payload}")
    
    local download_id
    download_id=$(echo "${response}" | jq -r '.id')
    
    if [ -z "${download_id}" ] || [ "${download_id}" == "null" ]; then
        log_error "Failed to create download"
        echo "Response: ${response}" | jq '.'
        exit 1
    fi
    
    log_success "Download created with ID: ${download_id}"
    
    # Display initial download details
    log_info "Download details:"
    echo "${response}" | jq '{
        id: .id,
        url: .url,
        fileName: .fileName,
        fileSize: .fileSize,
        state: .state
    }'
    
    # Step 2: Wait for X seconds
    log_step "Step 2: Waiting for ${WAIT_SECONDS} seconds to allow download progress"
    
    for ((i=${WAIT_SECONDS}; i>0; i--)); do
        echo -ne "  ${i} seconds remaining...\r"
        sleep 1
    done
    echo -e "  ${GREEN}✓${NC} Wait complete                    "
    
    # Check current progress
    log_info "Checking current download status..."
    local status_response
    status_response=$(curl -s "${API_ENDPOINT}/${download_id}")
    
    local current_state
    current_state=$(echo "${status_response}" | jq -r '.state')
    
    log_info "Current state: ${current_state}"
    
    if echo "${status_response}" | jq -e '.progress' > /dev/null 2>&1; then
        log_info "Current progress:"
        echo "${status_response}" | jq '.progress | {
            bytesDownloaded: .bytesDownloaded,
            totalBytes: .totalBytes,
            percentComplete: .percentComplete
        }'
    fi
    
    # Step 3: Pause the download
    log_step "Step 3: Pausing download"
    
    local pause_response
    pause_response=$(curl -s -w "\n%{http_code}" -X POST "${API_ENDPOINT}/${download_id}/pause")
    
    local pause_status_code
    pause_status_code=$(echo "${pause_response}" | tail -n1)
    
    if [ "${pause_status_code}" -eq 204 ]; then
        log_success "Download paused successfully"
    else
        log_error "Failed to pause download (HTTP ${pause_status_code})"
        echo "${pause_response}" | head -n-1
        exit 1
    fi
    
    # Give the system a moment to persist state
    sleep 1
    
    # Step 4: Read bytes downloaded from state file
    log_step "Step 4: Reading download progress from state file"
    
    local state_file="${STATE_DIR}/${download_id}.json"
    
    log_info "Looking for state file: ${state_file}"
    
    if [ ! -f "${state_file}" ]; then
        log_warning "State file not found at ${state_file}"
        log_warning "This may be expected if the state directory is different"
        log_info "Checking alternative location: ~/.kurio/state/${download_id}.json"
        
        state_file="${HOME}/.kurio/state/${download_id}.json"
        
        if [ ! -f "${state_file}" ]; then
            log_warning "State file not found at alternative location either"
            log_info "Falling back to API response for progress information"
            
            # Get progress from API
            local api_progress
            api_progress=$(curl -s "${API_ENDPOINT}/${download_id}")
            
            local bytes_downloaded
            bytes_downloaded=$(echo "${api_progress}" | jq -r '.progress.bytesDownloaded // 0')
            
            log_info "Bytes downloaded (from API): ${bytes_downloaded}"
        else
            log_success "State file found at: ${state_file}"
            
            local bytes_downloaded
            bytes_downloaded=$(jq -r '.bytesDownloaded // 0' "${state_file}")
            
            log_success "Bytes downloaded (from state file): ${bytes_downloaded}"
            
            # Display additional state information
            log_info "State file contents:"
            jq '.' "${state_file}" | head -n 20
        fi
    else
        log_success "State file found at: ${state_file}"
        
        local bytes_downloaded
        bytes_downloaded=$(jq -r '.totalBytesDownloaded // 0' "${state_file}")
        
        log_success "Bytes downloaded (from state file): ${bytes_downloaded}"
        
        # Display additional state information
        log_info "State file contents:"
        jq '.' "${state_file}"
    fi
    
    # Step 5: Prompt user to resume
    log_step "Step 5: Resume download"
    
    echo -e "${YELLOW}Press Enter to resume the download...${NC}"
    read -r
    
    # Step 6: Resume the download
    log_info "Resuming download..."
    
    local resume_response
    resume_response=$(curl -s -w "\n%{http_code}" -X POST "${API_ENDPOINT}/${download_id}/resume")
    
    local resume_status_code
    resume_status_code=$(echo "${resume_response}" | tail -n1)
    
    if [ "${resume_status_code}" -eq 204 ]; then
        log_success "Download resumed successfully"
    else
        log_error "Failed to resume download (HTTP ${resume_status_code})"
        echo "${resume_response}" | head -n-1
        exit 1
    fi
    
    # Final status check
    log_step "Final Status"
    
    log_info "Waiting 2 seconds for progress..."
    sleep 2
    
    local final_status
    final_status=$(curl -s "${API_ENDPOINT}/${download_id}")
    
    log_info "Download status:"
    echo "${final_status}" | jq '{
        id: .id,
        fileName: .fileName,
        state: .state,
        progress: .progress
    }'
    
    log_step "Test Complete"
    
    log_success "Pause/Resume test completed successfully!"
    log_info "Download ID: ${download_id}"
    log_info "You can monitor progress with: curl ${API_ENDPOINT}/${download_id} | jq '.'"
    log_info "State file location: ${state_file}"
}

# Run main function
main "$@"
