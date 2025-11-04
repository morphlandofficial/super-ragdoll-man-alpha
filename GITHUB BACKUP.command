#!/bin/bash

# Colors for pretty output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

GITHUB_REPO="https://github.com/morphlandofficial/super-ragdoll-man-alpha.git"

echo -e "${BLUE}================================${NC}"
echo -e "${BLUE}  PROJECT BACKUP TO GITHUB${NC}"
echo -e "${BLUE}================================${NC}"
echo ""

# Navigate to the directory where this script is located
cd "$(dirname "$0")"

# Get the project folder name
PROJECT_NAME=$(basename "$(pwd)")
echo -e "${BLUE}Project: ${NC}$PROJECT_NAME"
echo ""

# Check if .git folder exists, if not, initialize and connect to GitHub
if [ ! -d ".git" ]; then
    echo -e "${YELLOW}Setting up Git connection...${NC}"
    git init
    git remote add origin "$GITHUB_REPO"
    
    echo -e "${BLUE}Downloading existing backup history from GitHub...${NC}"
    git fetch origin
    
    # Check if we have changes compared to GitHub
    echo -e "${BLUE}Syncing with GitHub...${NC}"
    git checkout -b main
    git branch --set-upstream-to=origin/main main
    
    echo -e "${GREEN}✓ Connected to GitHub!${NC}"
    echo ""
fi

# Check if there are any changes
if [[ -z $(git status -s) ]]; then
    echo -e "${YELLOW}No changes detected. Nothing to backup!${NC}"
    echo ""
    echo "Press Enter to close..."
    read
    exit 0
fi

# Show what's being backed up
echo -e "${BLUE}Files changed:${NC}"
git status -s
echo ""

# Add all changes
echo -e "${BLUE}Adding files...${NC}"
git add .
echo ""

# Ask for update notes
echo -e "${YELLOW}What did you change? (Describe your updates)${NC}"
echo -n "> "
read UPDATE_NOTES

# If they didn't type anything, use a default message
if [ -z "$UPDATE_NOTES" ]; then
    TIMESTAMP=$(date "+%Y-%m-%d %H:%M:%S")
    COMMIT_MESSAGE="Auto backup: $TIMESTAMP"
else
    COMMIT_MESSAGE="$UPDATE_NOTES"
fi

echo ""
echo -e "${BLUE}Creating save point...${NC}"
git commit -m "$COMMIT_MESSAGE"

# Push to GitHub
echo ""
echo -e "${BLUE}Uploading to GitHub...${NC}"
git push -u origin main

# Check if push was successful
if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}================================${NC}"
    echo -e "${GREEN}✓ SUCCESS!${NC}"
    echo -e "${GREEN}Your project is backed up!${NC}"
    echo -e "${GREEN}================================${NC}"
else
    echo ""
    echo -e "${RED}================================${NC}"
    echo -e "${RED}✗ UPLOAD FAILED${NC}"
    echo -e "${RED}Check the error above${NC}"
    echo -e "${RED}================================${NC}"
fi

echo ""
echo "Press Enter to close..."
read
# Test change
