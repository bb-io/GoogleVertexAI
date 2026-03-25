# Blackbird.io Google Gemini AI

Blackbird is the new automation backbone for the language technology industry. Blackbird provides enterprise-scale automation and orchestration with a simple no-code/low-code platform. Blackbird enables ambitious organizations to identify, vet and automate as many processes as possible. Not just localization workflows, but any business and IT process. This repository represents an application that is deployable on Blackbird and usable inside the workflow editor.

## Introduction

<!-- begin docs -->

Gemini is a cutting-edge, multimodal AI from Google, capable of reasoning across various media, driving chatbots, search enhancements, productivity tools, and device-level interactions.

## Before setting up

Before you can connect you need to make sure that:

- You have selected or created a [Cloud Platform project](https://console.cloud.google.com/project).
- You have [enabled billing](https://cloud.google.com/billing/docs/how-to/modify-project) for your project.
- You have [enabled the Vertex AI API](https://console.cloud.google.com/flows/enableapi?apiid=aiplatform.googleapis.com).
- If you want to use Gemini File Search, you have also [enabled the Generative Language API](https://console.developers.google.com/apis/api/generativelanguage.googleapis.com/overview).
- You have either created a service account and generated JSON keys, or created a Gemini API key, but keep in mind that Gemini File Search is only supported with the Gemini API key authentication.

### Creating service account and generating JSON keys

1. Navigate to the selected or created [Cloud Platform project](https://console.cloud.google.com/project).
2. Go to the _IAM & Admin_ section.
3. On the left sidebar, select _Service accounts_.
4. Click _Create service account_.
5. Enter a service account name and, optionally, a description. Click _Create and continue_. Select the _Vertex AI Administrator_ or _Vertex AI User_ role for the service account and click _Continue_. 
![Creating service account](image/README/access.png)
6. . Click _Done_.
7. From the service accounts list, select the newly created service account and navigate to the _Keys_ section.
8. Click _Add key_ => _Create new key_. Choose the _JSON_ key type and click _Create_.
9. Open the downloaded JSON file and copy its contents, which will be used in the _Service account configuration string_ connection parameter.

### Creating a Gemini API key

1. Navigate to the selected or created [Cloud Platform project](https://console.cloud.google.com/project).
2. Open _APIs & Services_ -> _Credentials_.
3. Click _Create credentials_ -> _API key_.
4. Copy the created key and use it in the _API key_ connection parameter.
5. Optionally restrict the key to the _Generative Language API_.

## Connecting

1. Navigate to apps and search for Google Gemini AI. If you cannot find Google Gemini AI then click _Add App_ in the top right corner, select Google Gemini AI and add the app to your Blackbird environment.
2. Click _Add Connection_.
3. Name your connection for future reference e.g. 'My organization'.
4. Choose one of the supported connection types:
   - _Service account_: provide the _Service account configuration string_ and choose a region.
   - _Gemini API key_: provide the _API key_.
5. Click _Connect_.
6. Confirm that the connection has appeared and the status is _Connected_.

![Connecting](image/README/connecting.png)

## Authentication modes

- **Service account**: use this mode for Vertex AI based actions, background jobs, file attachments in prompt generation, and the existing regional Vertex flows.
- **Gemini API key**: use this mode for Gemini Developer API access, including Gemini File Search retrieval.

> **Important**: searching across File Search stores is supported only when the connection uses **Gemini API key** authentication. This applies to both the _Generate text_ action when _File search store names_ are provided and the _Search documents_ action.

## Actions

### Generation

- **Chat** Ask a question or give a command to Gemini. The input will be used as a prompt for the model. Also specify store names to search for relevant files to include in the response generation. Generation will be performed with the chosen model.
- **Chat with files**  similar to the previous action, but with more control over how retrieved file content is included in the prompt. You can specify a custom prompt template and use variables to include retrieved content and the original question in the prompt.

### File search

- **Create file search store** creates a Gemini File Search store.
- **Upload file to store** uploads a Blackbird file directly to a File Search store, optionally with metadata and custom chunking.
- **Search documents** queries one or more File Search stores and returns a grounded answer with retrieved contexts. This action is available only with a _Gemini API key_ connection.
- **Delete file search store** deletes a File Search store.

> This group of actions do not communicate with LLM, it's only about storing and retrieving files. The retrieved files can then be used in the prompt for the generation actions.

### Translation

- **Translate** translate file content retrieved from a CMS or file storage. The output can be used in compatible Blackbird interoperable actions.
- **Translate in background** similar to the previous action, except that this creates a long running process in the background not blocking your parallel actions. Use in conjunction with a checkpoint to download the result file of this long running background job.
- **Translate text** given a text and a locale, tries to create a localized version of the text.

### Editing

- **Edit** Edit a translation. This action assumes you have previously translated content in Blackbird through any translation action. Only looks at translated segments and will change the segment state to reviewed.
- **Edit in background** similar to the previous action, except that this creates a long running process in the background not blocking your parallel actions. Use in conjunction with a checkpoint to download the result file of this long running background job.
- **Edit Text** given a source segment and translated target segment, responds with an edited version of the target segment taking into account typical mistakes.

- > Note: files are interoperable, under the hood Blackbird may convert to and from bilingual files with metadata but all our Actions are able to accept a lot of different file formats.

### Reporting

- **Create MQM report** Perform an LQA Analysis of a translated XLIFF file. The result will be a text in the MQM framework form.
- **Create MQM report in background** similar to the previous action, except that this creates a long running process in the background not blocking your parallel actions. Use in conjunction with a checkpoint to download the result of this long running background job.

### Review

- **Quality estimation (experimental)** evaluates unit and file level translation quality for translated files.

### Background

- **Download background file** use this action to get the output file when doing a translation or edit in the background.
- **Get background result** use this action to get the output text when generating an MQM report in the backgorund.

## Events

### Background

- **On background job finished** triggered when a background job finishes. Use this after stating any background job, then use the applicable action to get the results.

### Temperatures

Gemini allows you to set the temperature value for and Action you perform in Blackbird. You can customize this value (between 0.0 and 1.0) but we've also added handy labels that are available in the Blackbird UI:

- 0.1 Governed: Maximum predictability.
- 0.3 Balanced: A controlled blend of accuracy and flexibility.
- 0.5 Expressive: Accuracy is preserved, structure is looser (default).
- 0.6 Exploratory: Encourages alternative phrasings and ideas while remaining context-aware.
- 0.8 Experimental: Prioritizes creativity over predictability.

### Important Notes on File Processing

> **Performance with Large Files**: Based on our experience, Gemini models may struggle with files containing more than 100 translation units. Performance can be inconsistent - sometimes working well but often producing hallucinations (returning only one translation unit when 50 were sent) or breaking formatting. For more reliable XLIFF processing, we recommend using background processing. Also, consider specifying a `Bucket size` of 15-20 or lower to ensure the model can handle the workload effectively.

### Notes

> **In order to work with background actions and events, following permissions are needed**:  `aiplatform.batchPredictionJobs.create`, `aiplatform.batchPredictionJobs.get`, `storage.buckets.create`


### Bucket size, performance and cost

Files can contain a lot of segments. Each action takes your segments and sends them to the AI app for processing. It's possible that the amount of segments is so high that the prompt exceeds the model's context window or that the model takes longer than Blackbird actions are allowed to take. This is why we have introduced the bucket size parameter. You can tweak the bucket size parameter to determine how many segments to send to the AI model at once. This will allow you to split the workload into different API calls. The trade-off is that the same context prompt needs to be send along with each request (which increases the tokens used). From experiments we have found that a bucket size of 1500 is sufficient for models like gpt-4o. That's why 1500 is the default bucket size, however other models may require different bucket sizes.

## Feedback

Do you want to use this app or do you have feedback on our implementation? Reach out to us using the [established channels](https://www.blackbird.io/) or create an issue.

<!-- end docs -->
