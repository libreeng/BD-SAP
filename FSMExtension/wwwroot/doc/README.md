# FSM Extension for Onsight Connect

#### Notes
   * The term "customer" refers to the Onsight customer wishing to integrate their Onsight Connect account with their SAP FSM application.
   * The variable *\{fsmHost\}* refers to the datacenter where your FSM application is deployed.
     * e.g., "eu.coresuite.com" or "us.coresuite.com"

---

## Pre-requisites

   - The Onsight Connect extension operates on FSM Activities which are typically
      part of Service Calls.
    - There are two sets of persons who can be called via the Onsight Connect extension:
        - The Activity's Contact.
        - The Activity's assigned Responsibles. This is the person 
          or persons assigned to this Activity. Note that this is applicable to the FSM Web app only,
          as it is expected that the mobile app user is the person responsible for the selected Activity
          and therefore cannot call themselves.
        - To be reachable by Onsight, the Contact's and/or Responsibles' email addresses must be part of the customer's Onsight domain
          - Onsight external contacts are not currently supported, as there is no defined way of getting
            that external contact's SIP address from FSM.
    - **(FSM Web):** The FSM account administrator must generate a Client ID/Client Secret pair for each FSM coresystems datacenter
      in which their FSM app is hosted. These credentials will be used by the Onsight extension to communicate
      with the FSM APIs.
        - For example, if the customer has FSM hosted at eu.coresystems.net and us.coresystems.net, the administrator
          would need to generate two Client ID/Client Secret pairs, one for each datacenter.
    - **(FSM Web) [Optional]**: If desired, a 3rd-party OpenID Connect provider can be used with the Onsight extension.
      If enabled, the logged-in FSM web user's identity will be verified via an OpenID Connect login consent screen
      (displayed within the Onsight extension's plugin frame). This verifies that the FSM web user can be mapped to a known
      Onsight user account. To enable OpenID Connect verification, the following information from your identity provider is needed:
        - The authorize URL (e.g., https://my-idp.com/api/oauth2/v1/authorize)
        - The token URL (e.g., https://my-idp.com/api/oauth2/v1/token)
        - The user info URL (e.g.,  https://my-idp.com/api/oauth2/v1/userinfo)
        - The Client ID
        - The Client Secret

---

## Installation

   - Installation can be performed either of two ways:
        1) From the SAP FSM extension catalog: https://{fsmHost}/shell/#/foundational-services/extension-management/directory, **OR**
        2) Manaully, by clicking the "Add Extension" button and choosing "Manual" installation: https://{fsmHost}/shell/#/foundational-services/extension-management/extensions
            - For the Extension Access URL, use: https://fsm-extension-app.azurewebsites.net

---

## Post-Install Configuration

   ### Custom Fields (optional)  

   By default, the Onsight Connect extension will assume that the current Activity's Contact field is also the designated Remote Expert. In other words, this Contact will be callable by both the dispatcher (from the web UI) and the field worker (from the mobile UI). If this is not desirable, the extension can be configured to use custom fields, associated with the Activity's Equipment, in determining who the Remote Expert is, as noted in the following section.

   #### Defining Custom Fields in the Equipment DTO

   - Using FSM's administration pages, locate the Custom Fields page at *https://{fsmHost}/admin/accounts/{accountId}/companies/{companyId}/udfMetas*.
   - Click the Create button.
   - Enter the following values (using defaults for everything else) and click Save. This field will hold the Remote Expert's email address:
      * **Name**: OnsightRemoteExpertEmail
      * **Description**: Onsight Remote Expert Email
      * **Object Type**: Equipment
      * **Type**: String
   - Click Create again, using these values for the second field. This will be used to display the Remote Expert's name:
      * **Name**: OnsightRemoteExpertName
      * **Description**: Onsight Remote Expert Name
      * **Object Type**: Equipment
      * **Type**: String
   - You will need to edit any existing Equipment by setting values for these two custom fields. Likewise, any new Equipment that is subsequently added will also need these fields to be set
   to the Remote Expert's email address and name, respectively.


   ### Mobile

   The FSM mobile app (available for download from the app stores) can be customized to integrate Onsight Connect.
   This is done by adding a new step to the field technician's Service Workflow process. At this time,
   configuration of the FSM mobile app for use with Onsight Connect is a manual process which must be
   performed by the FSM administrator.

   The following steps must be performed in order to activate the mobile extension:

   1) A new or existing Service Workflow must be modified to include a new Workflow Step:
    https://{fsmHost}/admin/accounts/{accountId}/companies/{companyId}/serviceWorkflows
   2) The new Workflow Step should be placed in an appropriate location within the Workflow and be connected to any succeeding steps. The image below illustrates an example of a new "Onsight Connect" Workflow Step inserted into a default Workflow, after the "work" step and before the "checkout" step:

         ![Edit Service Workflow](../images/edit-service-workflow.png)

   3) The new Workflow Step should be set to a Screen Type of "External application".
   4) The Workflow Step's Configuration should be set to one of the two following options, depending on where the designated Remote Expert information is located:
        - Option 1: when the Remote Expert is associated with the __Activity's Equipment__ (see above for details):
        ---
        **NOTE:** If you are NOT using the same UDF names shown above (i.e., <i>OnsightRemoteExpertEmail</i> or <i>OnsightRemoteExpertName</i>, you MUST substitute your UDF names for those names in the JSON snippet below):
         ```
        {
           "android": {"url": "https://fsm-extension-app.azurewebsites.net/FsmMobileIndex?from=${activity.responsibles[0].emailAddress}&to=${activity.equipment.udfValues.find(udf => udf.meta.name == 'OnsightRemoteExpertEmail').value}&toFirst=${activity.equipment.udfValues.find(udf => udf.meta.name == 'OnsightRemoteExpertName').value}&meta=eqp:${activity.equipment.code};act:${activity.code}"},
           "ios": {"url": "https://fsm-extension-app.azurewebsites.net/FsmMobileIndex?from=${activity.responsibles[0].emailAddress}&to=${activity.equipment.udfValues.find(udf => udf.meta.name == 'OnsightRemoteExpertEmail').value}&toFirst=${activity.equipment.udfValues.find(udf => udf.meta.name == 'OnsightRemoteExpertName').value}&meta=eqp:${activity.equipment.code};act:${activity.code}"}
        }
         ```
        - Option 2: when the Remote Expert is the __Activity's Contact__:
         ```
        {
           "android": {"url": "https://fsm-extension-app.azurewebsites.net/FsmMobileIndex?from=${activity.responsibles[0].emailAddress}&to=${activity.contact.emailAddress}&toFirst=${activity.contact.firstName}&toLast=${activity.contact.lastName}&meta=eqp:${activity.equipment.code};act:${activity.code}"},
           "ios": {"url": "https://fsm-extension-app.azurewebsites.net/FsmMobileIndex?from=${activity.responsibles[0].emailAddress}&to=${activity.contact.emailAddress}&toFirst=${activity.contact.firstName}&toLast=${activity.contact.lastName}&meta=eqp:${activity.equipment.code};act:${activity.code}"}
        }
         ```


   ### Web

   In order to complete installation of the Onsight extension with the FSM web app, the
   customer must contact Librestream to have their FSM credentials added to the extension's database. Without
   this configuration, the FSM web app will not work with the FSM extension.

   Once the customer has been added to the Onsight extension database, the FSM administrator must enable
   the extension in the web UI. This is done through the standard FSM extension mechanism:
   
   - Click "Open Extension Configuration" from the "..." menu in the upper-right-hand corner of FSM,
     which will place the UI into "Configuration Mode".
   - On the right-hand-side, under the "PLUG-IN" section, click the "Add Extension" button.
   - From the list of extensions, click "onsight-connect" and then the "Add" button.
   - Finally, click the "Configuration Mode" text at the top to return to normal mode.
