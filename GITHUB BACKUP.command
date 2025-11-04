#!/bin/bash

# Colors for pretty output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

GITHUB_REPO="https://github.com/morphlandofficial/super-ragdoll-man-alpha.git"

echo -e "${BLUE}================================${NC}"
echo -e "${BLUE}   BACKUP GAME TO GITHUB${NC}"
echo -e "${BLUE}================================${NC}"
echo ""

# Navigate to the directory where this script is located
cd "$(dirname "$0")"

PROJECT_NAME=$(basename "$(pwd)")
echo -e "${BLUE}Project: ${NC}$PROJECT_NAME"
echo ""

# First time setup - clone from GitHub if needed
if [ ! -d ".git" ]; then
    echo -e "${YELLOW}First time setup...${NC}"
    echo -e "${BLUE}Downloading existing backups from GitHub...${NC}"
    
    # Clone the repo into a temp folder
    cd ..
    TEMP_CLONE="temp_clone_$$"
    git clone "$GITHUB_REPO" "$TEMP_CLONE" 2>/dev/null
    
    # Move .git folder into our game folder
    if [ -d "$TEMP_CLONE/.git" ]; then
        mv "$TEMP_CLONE/.git" "$PROJECT_NAME/.git"
        rm -rf "$TEMP_CLONE"
        cd "$PROJECT_NAME"
        echo -e "${GREEN}✓ Connected to GitHub!${NC}"
        echo -e "${YELLOW}Your local files will be backed up as a new version.${NC}"
    else
        # No repo exists yet, create new one
        cd "$PROJECT_NAME"
        git init
        git remote add origin "$GITHUB_REPO"
        git checkout -b main
        echo -e "${GREEN}✓ Created new backup repository!${NC}"
    fi
    echo ""
fi

# Sync with GitHub to get latest backups first
echo -e "${BLUE}Syncing with GitHub...${NC}"
git fetch origin main 2>/dev/null
if git rev-parse origin/main >/dev/null 2>&1; then
    # Try to merge remote changes
    git merge origin/main --no-edit -m "Sync with GitHub" 2>/dev/null
    if [ $? -ne 0 ]; then
        echo -e "${YELLOW}Note: Using local version for any conflicts${NC}"
        git merge --abort 2>/dev/null
        # Use local files for conflicts (prefer ours)
        git merge origin/main -X ours --no-edit -m "Sync with GitHub" 2>/dev/null
    fi
fi
echo ""

# Check for changes
echo -e "${BLUE}Checking for changes...${NC}"
git add -A

# Check if there's anything to commit
if git diff --staged --quiet; then
    echo -e "${YELLOW}No changes detected - everything is already backed up!${NC}"
    echo ""
    echo "Press Enter to close..."
    read
    exit 0
fi

echo -e "${GREEN}✓ Found changes to backup${NC}"
echo ""

# Ask for backup notes
echo -e "${YELLOW}What did you work on today?${NC}"
echo -n "> "
read NOTES

# If they didn't type anything, use timestamp
if [ -z "$NOTES" ]; then
    TIMESTAMP=$(date "+%Y-%m-%d %H:%M")
    COMMIT_MESSAGE="Backup: $TIMESTAMP"
else
    COMMIT_MESSAGE="$NOTES"
fi

echo ""
echo -e "${BLUE}Creating backup entry...${NC}"
git commit -m "$COMMIT_MESSAGE"

# Push to GitHub
echo ""
echo -e "${BLUE}Uploading to GitHub...${NC}"
git push -u origin main

# Check if successful
if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}================================${NC}"
    echo -e "${GREEN}✓ BACKUP COMPLETE!${NC}"
    echo -e "${GREEN}Your game is saved to GitHub!${NC}"
    echo -e "${GREEN}================================${NC}"
else
    echo ""
    echo -e "${RED}================================${NC}"
    echo -e "${RED}✗ BACKUP FAILED${NC}"
    echo -e "${RED}Check the error above${NC}"
    echo -e "${RED}================================${NC}"
fi

echo ""
echo "Press Enter to close..."
read
