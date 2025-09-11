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
- You have created a service account and generated JSON keys.

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

## Connecting

1. Navigate to apps and search for Google Gemini AI. If you cannot find Google Gemini AI then click _Add App_ in the top right corner, select Google Gemini AI and add the app to your Blackbird environment.
2. Click _Add Connection_.
3. Name your connection for future reference e.g. 'My organization'.
4. Fill in the JSON configuration string obtained in the previous step.
5. Choose the necessary region.
6. Click _Connect_.
7. Confirm that the connection has appeared and the status is _Connected_.

![Connecting](image/README/connecting.png)

## Actions

### Generation

- **Generate text** generates text using Gemini model. Optionally, you can specify an image or video to perform generation with the gemini-1.0-pro-vision model. Both image and video have a size limit of 20 MB. If an image is already present, video cannot be specified and vice versa. Supported image formats are PNG and JPEG, while video formats include MOV, MPEG, MP4, MPG, AVI, WMV, MPEGPS, and FLV. Optionally, set _Is Blackbird prompt_ to _True_ to indicate that the prompt given to the action is the result of one of AI Utilities app's actions. You can also specify safety categories in the _Safety categories_ input parameter and respective thresholds for them in the _Thresholds for safety categories_ input parameter. If one list has more items than the other, extra ones are ignored.
- **Generate text from files** generates text using Gemini model with one or more additional file attached to the conversation.

### Translation

- **Translate** translate file content retrieved from a CMS or file storage. The output can be used in compatible Blackbird interoperable actions.
- **Translate in background** similar to the previous action, except that this creates a long running process in the background not blocking your parallel actions. Use in conjunction with a checkpoint to download the result file of this long running background job.
- **Translate text** given a text and a locale, tries to create a localized version of the text.

### Editing

- **Edit** Edit a translation. This action assumes you have previously translated content in Blackbird through any translation action. Only looks at translated segments and will change the segment state to reviewed.
- **Edit in background** similar to the previous action, except that this creates a long running process in the background not blocking your parallel actions. Use in conjunction with a checkpoint to download the result file of this long running background job.
- **Edit Text** given a source segment and translated target segment, responds with an edited version of the target segment taking into account typical mistakes.

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

### Important Notes on XLIFF Processing

> **Performance with Large XLIFF Files**: Based on our experience, Gemini models may struggle with XLIFF files containing more than 100 translation units. Performance can be inconsistent - sometimes working well but often producing hallucinations (returning only one translation unit when 50 were sent) or breaking formatting. For more reliable XLIFF processing, we recommend using background processing. Also, consider specifying a `Bucket size` of 15-20 or lower to ensure the model can handle the workload effectively.

### Notes

> **In order to work with background actions and events, following permissions are needed**:  `aiplatform.batchPredictionJobs.create`, `aiplatform.batchPredictionJobs.get`, `storage.buckets.create`


### Bucket size, performance and cost

XLIFF files can contain a lot of segments. Each action takes your segments and sends them to the AI app for processing. It's possible that the amount of segments is so high that the prompt exceeds the model's context window or that the model takes longer than Blackbird actions are allowed to take. This is why we have introduced the bucket size parameter. You can tweak the bucket size parameter to determine how many segments to send to the AI model at once. This will allow you to split the workload into different API calls. The trade-off is that the same context prompt needs to be send along with each request (which increases the tokens used). From experiments we have found that a bucket size of 1500 is sufficient for models like gpt-4o. That's why 1500 is the default bucket size, however other models may require different bucket sizes.

## Feedback

Do you want to use this app or do you have feedback on our implementation? Reach out to us using the [established channels](https://www.blackbird.io/) or create an issue.

<!-- end docs -->
