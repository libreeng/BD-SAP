# FSM Extension Design Notes

This repo contains an implementation of a Librestream Onsight Connect and Workspace extension for the SAP Field Service Manager (FSM) application.
It provides both the frontend and backend components for the extension within both the web and mobile versions of FSM.

For a high-level overview of this extension's uses and requirements, [see the README](./wwwroot/doc/README.md).

The extension is written as an ASP.NET Core 3.1 application and can be packaged into Docker container.

## appconfig.json

In order to be installable within FSM, the backend must host an *appconfig.json* from the root of the extension's backend server.
The file contains FSM Extension app configuration details and can be customized as needed.
This implementation's *appconfig.json* is located at *wwwroot/appconfig.json*.

## domain_mapping table

The extension's configuration data is persisted in a JSON document database (Azure Cosmos DB, to be precise).
This data could easily be stored in a relational database if needed.

Each entry in the domain_mapping table represents a Librestream customer integration with their FSM account.

```
    "id": "b114da8e-cddd-4985-b214-d19b5d98397c",
    "onsightDomain": "example.com",
    "emailUsers": [
        "user.one",
        "user.two"
    ],
    "onsightApiKey": "...",
    "sap_fsm": {
        "accountId": "12345",
        "accountName": "my-sap-fsm-account",
        "installs": [
            {
                "cloudHost": "eu.coresuite.com",
                "clientId": "aaaaaaaa-0000-aaaa-0000-a0a0a0a0a0a0",
                "clientSecret": "...",
                "clientVersion": "1.0"
            }
        ],
        "companies": [
            {
                "id": "999999",
                "identityProvider": {
                    "authorizeUrl": "https://my-idp.com/api/oauth2/v1/authorize",
                    "tokenUrl": "https://my-idp.com/api/oauth2/v1/token",
                    "userInfoUrl": "https://my-idp.com/api/oauth2/v1/userinfo",
                    "clientId": "bbbbbbbb-0000-bbbb-0000-b0b0b0b0b0b0",
                    "clientSecret": "..."
                }
            }
        ],
        "customization": {
            "remoteExpert": {
                "email": "NameOfMyCustomFieldReferencingTheRemoteExpertEmailAddress",
                "name": "NameOfMyCustomFieldReferencingTheRemoteExpertName"
            }
        }
    }
```
| Field       | Description |
| :---------- | :---------- |
| id                   | Auto-generated internal mapping ID. |
| onsightDomain        | The customer's Onsight Connect domain |
| emailUsers           | The list of users (by email name) registered to use the extension. The extension assumes each registered user's email domain matches their Onsight domain.  |
| onsightApiKey        | The customer's Onsight Connect API Key. |
| sap_fsm              | The customer's FSM account details. |
| &nbsp;&nbsp;&nbsp;&nbsp;accountId    | The customer's FSM account identifier. Required to call the FSM APIs. |
| &nbsp;&nbsp;&nbsp;&nbsp;accountName  | The customer's account name. Required to call the FSM APIs. |
| &nbsp;&nbsp;&nbsp;&nbsp;installs[]   | One entry for each datacenter in which the customer has a SAP FSM installation. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;cloudHost | The cloud host/datacenter name where FSM is installed. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;clientId | Customer-generated FSM Client ID used to call FSM APIs. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;clientSecret | Customer-generated FSM Client Secret used to call FSM APIs. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;clientVersion | Always "1.0". |
| &nbsp;&nbsp;&nbsp;&nbsp;companies[]  | One entry for each FSM company (within the customer's FSM account) for which the extension will be available. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;id | The FSM company number. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;identityProvider | **[Optional]** If non-null, specifies the 3rd-party OpenID Connect provider details for verifying the FSM web user's Onsight account. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;authorizeUrl | The identity provider's /authorize URL. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;tokenUrl | The identity provider's /token URL. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;userInfoUrl | The identity provider's /userinfo URL. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;clientId | Client ID to use when calling the identity provider's APIs. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;clientSecret | Client Secret to use when calling the identity provider' APIs. |
| &nbsp;&nbsp;&nbsp;&nbsp;customization | **[Optional]** Section detailing custom field names. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;remoteExpert | Custom field names which map to the designated Remote Expert. These custom fields are attached to the Activity's Equipment DTO. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;email | UDF name holding the Remote Expert's email address. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;name | UDF name holding the Remote Expert's name. |

## Backend

The majority of the work occurs on the backend server. It is responsible for:
  - OpenID verification (if enabled)
  - Getting and returning the responsible(s) and contact names and addresses for the selected Activity
    - */api/v1/fsm/connections*
  - Responding to requests to begin an Onsight Connect session
    - */api/v1/fsm/connection*
  - Retrieving details about the selected Activity
    - */api/v1/fsm/activity*
  - Retrieving a list of Onsite Workspace documents
    - */api/v1/fsm/workspace-documents*

#### Authentication Notes

The backend provides four public APIs for use by the extension frontend:
  1) *GET /api/v1/fsm/connection*: takes a caller's email address and a callee's email address,
returning an Onsight Connect URI. Opening the returned URI will open Onsight connect and initiate the call.
      - This API is used by both the mobile and web frontends and as such must be available without authentication.
        This is due to the restricted nature of the FSM mobile app configuration which does not provide a
        code sandbox or Shell SDK access the way that the FSM web app does.
      - While the API itself is not protected, the Onsight Connect application will still require
      all participants to enter their credentials before the call can proceed.
  2) *GET /api/vi/fsm/connections*: takes a set of FSM account details AND an FSM Activity, returning
a list of */api/v1/fsm/connection* URLs corresponding to the Activity's assigned responsibles and contact, or FSM Activity details.
      - This API is used exclusively by the FSM web frontend and as such can be restricted to authenticated users only.
      - This API requires a JWT Bearer token which can be obtained by initiaing an authentication flow via */auth/provider*.
        - If the customer is configured to use a 3rd-party OpenID Connect provider, this will redirect the end-user to
        the provider's login/consent screen.
        - If no OpenID Connect provider is configured, a simple authentication is performed to ensure that the logged-in FSM
        user has a corresponding entry in the extension's database.
  3) *GET /api/v1/fsm/activity*: takes a set of FSM account details AND an FSM Activity,
returning FSM Activity Details.
      - This API is used exclusively by the FSM web frontend and as such can be restricted to authenticated users only.
  4) *GET /api/vi/fsm/workspace-documents*: takes the logged-in FSM users email address AND an FSM Activity code, returning
a list of Onsite Workspace documents corresponding to the FSM Activity Code. Opening the documents download URL will 
open Onsight Workspace for the selected document.
        - This API is used exclusively by the FSM web frontend and as such can be restricted to authenticated users only.        
        - While the API itself is not protected, the Onsight Workspace application will still require
      the logged-in FSM user to enter their credentials before viewing the Workspace document.
        - This API requires a JWT Bearer token which can be obtained by initiaing an authentication flow via */auth/provider*.
        - If the customer is configured to use a 3rd-party OpenID Connect provider, this will redirect the end-user to
        the provider's login/consent screen.
        - If no OpenID Connect provider is configured, a simple authentication is performed to ensure that the logged-in FSM
        user has a corresponding entry in the extension's database.

## Frontend

#### Web

The extension's web frontend uses the FSM shell SDK to retrieve the FSM user's account, company, and user IDs.
These credentials are passed to the backend to optionally verify the FSM user's identity via a 3rd-party OpenID Connect provider.

Once verified, the frontend makes requests to the backend (*/api/v1/fsm/connections*) to display available Onsight connections.

The 'Import Assets' button will populate an accordion menu of the workspace documents tagged with the currently selected activity code.
Each document item will provide details such as external metadata, document type, document title, and download URL from Onsite Workspace.

#### Mobile

The mobile frontend is similar in use to the web frontend, but due to the restricted nature of mobile applications
does not provide the same level of functionality:

  - There is no FSM Shell SDK available for mobile extensions.
    - No external verification via OpenID Connect is possible.
    - All Activity and contact/responsible information (such as names and email addresses) must be provided
      directly to the mobile extension's frontend as query parameters.
  - The mobile app user is assumed to be the field tech/responsible person for the given Activity.
    - The only person available for an Onsight Call becomes the designated expert.
  - The Workspace integration link will launch Onsite Workspace in a browser with a filter query 
  showing all documents tagged with the current activity code.
