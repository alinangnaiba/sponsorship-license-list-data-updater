# Data updater Background Job

This background job application is designed to automatically fetch, process, and store UK visa sponsorship data from the UK government website.

## Overview

The application downloads a CSV file from the UK government's "Register of Licensed Sponsors: Workers" page, processes the data, and stores it in a RavenDB database. It runs as a stand-alone .NET application that can be containerized using Docker. This application fetches and updates data for [UK licensed sponsors](https://uk-licensed-sponsors.vercel.app/) application.

## Core Functionality

1. **Web Crawling**: 
   - Scrapes the government website ([gov.uk](https://www.gov.uk/government/publications/register-of-licensed-sponsors-workers)) to obtain:
     - The last updated date of the data
     - The download link for the CSV file

2. **Intelligent Processing**:
   - Compares the last updated date with previous runs to avoid redundant processing
   - Downloads the CSV file only if newer data is available
   - Stores a copy of the file in cloud storage (Google Cloud Storage)

3. **Data Management**:
   - Parses the CSV containing organization data using CsvHelper
   - Groups organizations by name and consolidates their properties
   - Uses bulk operations for efficient database interaction
   - Tracks three types of changes:
     - Added organizations (new entries)
     - Updated organizations (changed details)
     - Deleted organizations (no longer in the dataset)

4. **Process Logging**:
   - Creates detailed process logs with:
     - Start/finish timestamps
     - Process status (Completed, Failed, In Progress, No Update)
     - Detailed error information when failures occur
     - Complete record of all data changes (additions, updates, deletions)

5. **Error Handling**:
   - Comprehensive error handling throughout the process
   - Detailed error logging with method names and stack traces

## Technical Implementation

- **Framework**: .NET 9.0
- **Database**: RavenDB (NoSQL document database)
- **Storage**: Google Cloud Storage for CSV file storage
- **CSV Parsing**: CsvHelper library
- **Web Scraping**: HtmlAgilityPack
- **Containerization**: Docker support

## Deployment

The application can be run as:
- A standalone .NET application
- A containerized Docker application

## Configuration

Configuration is provided via `appsettings.json` with the following settings:

- **DatabaseSettings**: RavenDB connection details
- **FileStorageSettings**: Google Cloud Storage bucket information
- **CrawlerSettings**: Government website URL to crawl

## Runtime Process

1. Application starts and initializes services
2. Checks for updates on the government website
3. If new data is available:
   - Downloads the CSV file
   - Processes the organization data
   - Updates the database (add/update/delete)
   - Creates a detailed process log
4. If no updates are available, sets status to "No Update"
5. Application completes with timing information
