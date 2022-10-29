# Statiq scrape

A simple utility to scrape a website and export markdown files to be used by Statiq.Web or other static site generator.

Usage `dotnet run <url> [options]`

## Options

    --output      Output directory
    --class       The class name of the html element to extract content from
    --trim-title  Text to trim from the end of each page title

## Front matter

The utility will extract some standard meta tags and output them as front matter in yaml format.  These include

- Title
- Description
- Keywords

As other front matter is likely to be dependant on the site being scraped, the method `ExtractFrontMatter` can be modified to find and extract front matter as needed.
