# Statiq scrape

A simple utility to scrape a website and export markdown files to be used by Statiq.Web or other static site generator.

Usage `dotnet run <url> [options]`

## Options

    --output      Output directory
    --class       The class name of the html element to extract content from
    --trim-title  Text to trim from the end of each page title