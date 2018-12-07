# Azure Static Site CLI

> WARNING: This is a proof of concept and isn't meant to be used in a production environment.

## Goal

The goal of this is CLI is to provide a scenario-focused application that helps developers provision, and deploy a static site on Azure.

## Usage

Once the initial release is completed, the goal is to have a minimal set of commands to create the resources.

### Authentication

```bash
azstatic login
```

Running the `login` command will open up a browser page, allow you to login to Azure with your credentials. 

### Initialization

```bash
azstatic init
```

Running the `init` command will launch you through a wizard that will send you through the following steps.

1. Select a subscription (if you have more than one)
2. Pick a name for your website

The name of your website will impact the name of the Azure Resource Group as well as the Storage account that will be created.

When run successfully, the `init` command will create an `azure.json` file at the root of the site that will contain the resources name upon which this site is deployed.

### Deployment

```bash
azstatic deploy
```

The `deploy` command depends on the presence of `azure.json` and a valid token obtained from the the `login` command.

Running the `deploy` command will take all the files currently in the same folder as `azure.json` and upload them into the blob storage account defined in the `azure.json` file.

`deploy` will not push the `azure.json` file to blob storage.

## azure.json schema

```json
{
  "subscriptionId": "00000000-0000-0000-0000-000000000000",
  "resourceGroup": "<automatically-generated>",
  "storageName": "<automatically-generated>",
  "customDomain": "www.example.org"
}
```