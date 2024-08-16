# Blackbird.io Google Vertex AI

Blackbird is the new automation backbone for the language technology industry. Blackbird provides enterprise-scale automation and orchestration with a simple no-code/low-code platform. Blackbird enables ambitious organizations to identify, vet and automate as many processes as possible. Not just localization workflows, but any business and IT process. This repository represents an application that is deployable on Blackbird and usable inside the workflow editor.

## Introduction

<!-- begin docs -->

Vertex AI is a comprehensive platform offering access to powerful multimodal models like Gemini from Google, enabling developers to seamlessly combine various inputs such as text, images, video, or code. With a diverse selection of models, Vertex AI facilitates easy customization and integration, allowing for the development and deployment of AI applications. The platform provides generative AI models, fully managed tools, and purpose-built MLOps solutions to streamline the entire machine learning lifecycle—from training and tuning to deployment and monitoring.

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
5. Enter a service account name and, optionally, a description. Click _Create and continue_. Select the _Vertex AI Administrator_ role for the service account and click _Continue_. Finally, click _Done_.
6. From the service accounts list, select the newly created service account and navigate to the _Keys_ section.
7. Click _Add key_ => _Create new key_. Choose the _JSON_ key type and click _Create_.
8. Open the downloaded JSON file and copy its contents, which will be used in the _Service account configuration string_ connection parameter.

## Connecting

1. Navigate to apps and search for Google Vertex AI. If you cannot find Google Vertex AI then click _Add App_ in the top right corner, select Google Vertex AI and add the app to your Blackbird environment.
2. Click _Add Connection_.
3. Name your connection for future reference e.g. 'My organization'.
4. Fill in the project ID of the selected project and the JSON configuration string obtained in the previous step.
5. Click _Connect_.
6. Confirm that the connection has appeared and the status is _Connected_.

![Connecting](image/README/connecting.png)

## Actions

- **Generate text with Gemini** generates text using Gemini model. If text generation is based on a single prompt, it's executed with the gemini-1.0-pro model. Optionally, you can specify an image or video to perform generation with the gemini-1.0-pro-vision model. Both image and video have a size limit of 20 MB. If an image is already present, video cannot be specified and vice versa. Supported image formats are PNG and JPEG, while video formats include MOV, MPEG, MP4, MPG, AVI, WMV, MPEGPS, and FLV. Optionally, set _Is Blackbird prompt_ to _True_ to indicate that the prompt given to the action is the result of one of AI Utilities app's actions. You can also specify safety categories in the _Safety categories_ input parameter and respective thresholds for them in the _Thresholds for safety categories_ input parameter. If one list has more items than the other, extra ones are ignored.

- **Get Quality Scores for XLIFF file** Gets segment and file level quality scores for XLIFF files. Supports only version 1.2 of XLIFF currently. Optionally, you can add Threshold, New Target State and Condition input parameters to the Blackbird action to change the target state value of segments meeting the desired criteria (all three must be filled).

    Optional inputs:
	- Prompt: Add your criteria for scoring each source-target pair. If none are provided, this is replaced by _"accuracy, fluency, consistency, style, grammar and spelling"_.
	- Bucket size: Amount of translation units to process in the same request. (See dedicated section)
	- Source and Target languages: By defualt, we get these values from the XLIFF header. You can provide different values, no specific format required. 
	- Threshold: value between 0-10.
	- Condition: Criteria to filter segments whose target state will be modified.
	- New Target State: value to update target state to for filtered translation units.

    Output:
	- Average Score: aggregated score of all segment level scores.
	- Updated XLIFF file: segment level score added to extradata attribute & updated target state when instructed.

- **Post-edit XLIFF file** Updates the targets of XLIFF 1.2 files

Optional inputs:
	- Prompt: Add your linguistic criteria for postediting targets.
	- Bucket size: Amount of translation units to process in the same request. (See dedicated section)
	- Source and Target languages: By default, we get these values from the XLIFF header. You can provide different values, no specific format required.
	- Glossary
	- Update locked segments: If true, locked segments will be updated, otherwise they will be skipped. By default, this is set to false.

- **Process XLIFF file** given an XLIFF file, processes each translation unit according to provided instructions (default is to translate source tags) and updates the target text for each unit. Supports only version 1.2 of XLIFF currently.

### Bucket size, performance and cost

XLIFF files can contain a lot of segments. Each action takes your segments and sends them to OpenAI for processing. It's possible that the amount of segments is so high that the prompt exceeds to model's context window or that the model takes longer than Blackbird actions are allowed to take. This is why we have introduced the bucket size parameter. You can tweak the bucket size parameter to determine how many segments to send to OpenAI at once. This will allow you to split the workload into different OpenAI calls. The trade-off is that the same context prompt needs to be send along with each request (which increases the tokens used). From experiments we have found that a bucket size of 1500 is sufficient for gpt-4o. That's why 1500 is the default bucket size, however other models may require different bucket sizes.

## Feedback

Do you want to use this app or do you have feedback on our implementation? Reach out to us using the [established channels](https://www.blackbird.io/) or create an issue.

<!-- end docs -->
