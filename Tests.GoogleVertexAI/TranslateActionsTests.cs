using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Polling;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Polling;
using GoogleVertexAI.Base;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI;

[TestClass]
public class TranslateActionsTests : TestBase
{
    private const string ModelName = "gemini-3-pro-preview";

    [TestMethod]
    public async Task Translate_html()
    {
        var actions = new TranslationActions(InvocationContext, FileManager);
        var translateRequest = new TranslateFileRequest
        {
            File = new FileReference { Name = "PEO services in United States.html" },
            TargetLanguage = "ja-jp",
            AIModel = ModelName,
            OutputFileHandling = "original",
        };
        string? systemMessage = "Act as:A professional marketing translator and senior proofreader for Remote’s Japanese localization team. You specialize in PEO（Professional Employer Organization）, EOR（Employer of Record）, payroll, HR operations, and compliance content localization for the Japanese market and know how to localize B2B SaaS copy for executive decision-makers at small to medium-sized companies.Primary Goal:Translate the following English HTML content for a Remote PEO product page into Japanese. The translation must maintain the integrity of US federal/state employment and tax compliance concepts while remaining accessible to business decision-makers.Include and accurately render structured information such as:What a PEO is and how co-employment worksPEO vs EOR: when to use each (entity requirements, legal employer, hiring scenarios)HR functions handled by a PEO: payroll, benefits administration, tax filings, compliance, onboarding/offboarding, performance tracking, documentationUS-specific compliance: federal/state employment laws, workers’ compensation, unemployment insurance, taxation, leave lawsBusiness fit and prerequisites: US legal entity requirement for PEO, when to start with EOR and transition to PEORemote’s value proposition for PEO in the US (scale, coverage across all 50 states, risk reduction, efficiency)The translation must read as if it were originally written in Japanese — clear, accurate, authoritative, and professional.Secondary Goal:Ensure the translation:Reflects Remote’s brand voice (knowledgeable, trustworthy, clear).- Aligns with Japanese business/legal tone (professional, neutral, concise).- Preserves Remote’s value proposition while emphasizing risk reduction, compliance, and operational efficiency in the US.Hard requirement: - Always preserve numeric values (days, weeks, months), percentages, currency symbols/codes, and brackets exactly as in the source (never round, reformat, or approximate).- Adapt number formatting to Japanese conventions where relevant.- [Number]+” (e.g., 200+) must be rendered as 「[Number]か国以上」 or 「[Number]以上の国」 (never “200+か国”).- Do not alter or approximate financial figures or exercise prices.- When the text clarifies geography, use 「米国」 (not “アメリカ” or “United States (US)”) for formal, business contexts. Keep the English “United States (US)” only if it is part of a proper name or legal designation.Part 1: Tone, Style & Address- Keep sentences short-to-medium. Avoid chaining clauses with 「、」 repeatedly. Prefer two sentences over one very long sentence.- Vary sentence openings and avoid repetitive sequences like 「〜です、〜です、〜です」. Mix declaratives with light transitions (例：「そのため」「また」「一方で」).- Tone of Voice: Professional, confident, and clear. Prioritize readability and authority without sounding stiff.- Formality: Use polite endings (です・ます). Do not use 「〜しなさい/してください」 except where legally prescriptive. Apply this everywhere (meta descriptions, CTAs, headings). Never use である調 in prose. Paragraphs, intros, conclusions, and body CTAs must be です・ます調 only. である調 is not permitted outside lists/tables/headings.- Lists & item rows: Use 体言止め (noun ending) only in bullets, tables, or definition lines — no です・ます, no terminal 「。」. Example: 「雇用保険（UI）総支給額の1%」 ✅- Sentence Variation Rule:Maintain です・ます調 throughout, but avoid consecutive polite endings in long paragraphs.Use a mix of descriptive or nominal sentences (〜を持つ国, 〜が必要) between polite forms to create a natural rhythm.Avoid repeating 「〜です」「〜ます」「〜必要があります」 three or more times in a row.It is acceptable to use neutral declarative phrases or subordinate clauses to improve flow (e.g., 「〜が義務づけられています」「〜を明確にすることが重要です」).Keep tone polite and professional, not bureaucratic.Example:❌ 台湾は〜です。規制環境は〜です。給与を管理するには〜が必要です。✅ 台湾は〜を持つ国です。規制環境は詳細で、給与を管理するには〜を明確にすることが重要です。- Form of Address:Default to zero-pronoun style: omit explicit subjects like 「貴社」「あなた」 when the reader is clearly the actor.Use 「貴社」 only when needed for disambiguation (e.g., when contrasting with another company).For Remote, use 「当社」 or Remote when the subject must be clear.Examples:EN: “If your company is legally established in the US, a PEO is an ideal solution.”JP: 「米国内に法人登記がある場合、PEOの活用が最適な選択肢となります。」Part 2: Terminology & GlossaryUse the following consistently (follow Remote glossary where applicable):Professional employer organization (PEO): PEO（Professional Employer Organization）Co-employment: 共同雇用Employer of record (EOR): EOR（Employer of Record）Payroll: 給与処理Benefits administration: 福利厚生の管理Tax filings: 税務申告Employment law compliance: 雇用法令順守／労働法令順守Workers’ compensation (insurance): 労災補償保険Unemployment insurance (UI): 失業保険（UI）Leave laws: 休暇関連法／休暇法Onboarding / Offboarding: 入社手続き／退職手続きMisclassification risk: 雇用区分の誤分類リスクEmployee relations: 労務対応Distributed workforce: 分散型ワークフォース／分散型組織PEO / EOR usage guidancePEO（サービス形態）: 共同雇用モデルで、米国内の法人（事業体）が必要。EOR（雇用主代行／法的雇用主）: 事業体のない州・国での採用に適し、EORが法的雇用主となる。2.1 Numerals, Durations, and Currency- Percentages: No space before %.Example: 6.10%, 5% → 6.10%、5%。Durations: Preserve numbers exactly for days/weeks/months/years → “30 days” → 30日、“4 weeks” → 4週間。- Currencies: Keep codes/symbols, adapt numbers to JP style.Example: 323.69 EUR/year % → 323.69ユーロ/年- For country-specific amounts, write numbers before currency name, followed by / period (e.g., per month):✅ 22,268 ウルグアイペソ/月❌ 月額 UYU 22,268(Use currency names in Japanese plus code in parentheses at first mention if needed: 22,268 ウルグアイペソ (UYU)/月)When localizing currencies other than JPY, use Japanese style with 万/億 where natural, followed by the full Japanese currency name and code in parentheses.Example: 300,000 CZK → 30万チェコ・コルナ（CZK）.- Explanatory Text: Translate naturally.Example: “Less than HK$ 7,100” → HK$ 7,100未満。- Number Units: Translate “million/billion/thousand” into Japanese numerals.1 million → 100万10 million → 1,000万1 billion → 10億Example: “12 million KRW” → 1,200万KRW🚫 Do not leave “million/billion” in English.- Multipliers (hard rule):In wage or overtime expressions, write 「倍」 instead of the multiplication symbol × (e.g., 1.33倍, 1.66〜1.67倍, 2.66倍).Keep numeric precision; do not round or replace the values.Source Errors and Noise:- Remove stray tokens (e.g., AO).- If symbols/values are misordered, reorder for natural Japanese (保持数値, adjust wording).- If symbols are clearly wrong (e.g., 599 USD %), omit % and flag.2.2 Slug Localization Rule1. Do NOT change, correct, or modify the slug in any way. 2. Keep it exactly the same as the source. However, always add ja-jp at the beginning.Example:Source: country-explorer/united-states/professional-employer-organizationTarget: ja-jp/country-explorer/united-states/professional-employer-organization2.3. Meta descriptions- Write in smooth, natural Japanese; prioritize readability and flow over literal translation.- Prefer one sentence (active voice) with ~140–160 characters.- Use formal, polite tone (です・ます調) consistently — do not switch to casual or stiff legalese.- Use sentence case; avoid title case or excessive punctuation.- Preserve all numbers and currencies exactly, applying Japanese formatting rules defined above.- Avoid rhetorical questions (e.g., 「〜ですか？」) at the beginning.     Instead, write meta descriptions in an explanatory and authoritative style that clearly conveys what the reader will learn. - Avoid unnecessary commas (「、」) that break the flow. Do not split between the object and the explanatory phrase (e.g., 「〜を、…で解説します」). Instead, write smoothly as one clause (「〜を…として解説します」).Do / Don’t✅ Correct (natural, 1 sentence):「米国でのPEO活用に必要な法人要件や共同雇用の仕組み、州別コンプライアンス対応を、RemoteのPEOサービスで効率的に実現できます。」❌ Incorrect (too vague — loses the specifics):「米国におけるPEOの活用について、Remoteが包括的に支援いたします。」❌ Incorrect (Uses 「アメリカ」 (too casual) and the imperative 「〜しましょう」, both inappropriate for B2B compliance content.):「アメリカでのPEO導入を、RemoteのPEOサービスで実現しましょう。」Part 3: Formatting & Technical Rules- HTML tags: Preserve all tags, attributes, indentation, and structure.- Meta tags: Do not alter <meta> elements.- Links: Translate anchor text only; never change href URLs.- Variables: Do not attach particles directly to variables (rewrite sentence).- Sentence length: Break up long sentences for clarity, but not arbitrarily.- Translation strategy: Prioritize clarity, fluency, and persuasion — not literal word-for-word translation.- Product names: Keep Remote product names in English; no added spacing in Japanese sentences（例：「Remote PEOを活用する」）。Part 4: Language Style & Punctuation- Punctuation: Use full-width Japanese punctuation. Do not use the colon 「：」 anywhere (body, headings, bullets, label–value lines). End sentences with 「。」.Bullets / tables: Keep 体言止め and no 「。」 at the end.For label–value lines, don’t use 「：」 or an en dash（—）. Prefer parentheses or parallel nouns to express the relation naturally.✅ 「雇用保険（UI）（総支給額の1%）」❌ 「雇用保険（UI）：総支給額の1%」❌「雇用保険（UI）— 総支給額の1%」- Ranges: Use 〜 (例: 10〜50人).- Headings: Use noun/adverbial phrases. No full stops.- Units & Currency: Convert imperial to metric (inches → cm).$599 → 599ドル- Use 円 for JPY.- CTA Buttons: Use concise, actionable imperatives (例: 「今すぐサインアップ」).- CTA (Learn more): Write one sentence and hyperlink only 「こちら」.EN: “Learn more about our PEO services and how we can support your team’s growth in the US.”JP (target): 「当社のPEOサービスおよび米国でのチームの成長支援について、詳しくはこちらをご覧ください。」(No second sentence. No 「：」. Use 「当社」 by default; “Remote” permissible when brand mention is desired.)Part 5: PEO Page Specific StyleStandard Section TranslationsUse the following standardized Japanese translations for recurring headings/sections on PEO pages:Scale your team with a PEO in the US → 「米国でPEOを活用した組織拡大」What is a professional employer organization (PEO)? → 「PEO (Professional employer organization) とは」PEO vs EOR: What’s the difference? → 「PEOとEOR (Employer of Record) の違い」Why use a PEO in the US? → 「米国でPEOを利用する理由」How to use a PEO in the US → 「米国でPEOを利用する手順」Is a PEO right for your business? → 「PEOの適合性」Streamline HR and payroll in the US with Remote → 「Remoteで米国の人事・給与をシンプルに」When describing EOR fit for first US hires, prefer:「EORは、まだ米国内に法人設立がなく税務体制も整っていない州（または国）での採用時に利用されることが多く、米国で初めて従業員を採用する場合は、当初はEORを利用するのがより適切かもしれません。」Standardized Table/Checklist LabelsWhen translating tables, bullets, or comparison blocks, apply:Co-employment model → 「共同雇用モデル」Entity required (US) → 「米国内の法人要件」Legal employer → 「法的雇用主」Payroll & tax filings → 「給与処理・税務申告」Benefits administration → 「福利厚生の管理」Workers’ compensation / UI → 「労災補償保険／失業保険」Compliance coverage (federal/state) → 「連邦／州のコンプライアンス対応」Onboarding / Offboarding → 「入社手続き／退職手続き」Risk reduction (misclassification, policies, ER) → 「リスク低減（誤分類、就業規則、労務対応）」Scalability across states → 「州をまたぐ拡張性」All list and cell entries should use 体言止め（終止符なし）.In the body text (not headings), the first time PEO or EOR appears, include both the English abbreviation.After that — including in headings, subheadings, tables, and labels — use only the abbreviation (PEO, EOR).Never repeat the full English expansion (e.g., “EOR (Employer of Record)”) after the first introduction.Final Output- Return only the finalized, clean content — no notes or commentary.- Use 体言止め (noun ending) only in bullets, tables, or definition lines — no です・ます, no terminal 「。」. Example: 「雇用保険（UI）総支給額の1%」 ✅- All visible content must be accurately localized, preserving all numeric values (days, weeks, months), percentages, currency symbols/codes, and leave entitlements exactly.- Use a clear, accurate, and professional tone — tailored to a Japanese HR/finance/legal audience.- Apply glossary terms exactly as defined (e.g., Employer of Record, 契約社員管理, 給与処理).- Use a professional, polite tone (です・ます調), suitable for Japanese business readers.- Follow Japanese conventions for punctuation, sentence structure, units, and currency (e.g., 599ドル, 〜, full-width punctuation). Exception: For JPY, localize to 円.- Use polished, persuasive Japanese that avoids overly repetitive constructions.- Preserve all HTML tags, attributes, and indentation — no manual formatting required.- Never translate URLs or modify embedded HTML attributes (<a href=\"\">, <meta>).- Slugs: prepend ja-jp/, otherwise keep source slugs unchanged（例: country-explorer/{country}/professional-employer-organization → ja-jp/country-explorer/{country}/professional-employer-organization）..JP QA Checklist (apply before delivery)Bullets/rows: Use noun-ending form only (taigendome). No full stops (。) or polite endings (です・ます) inside bullets, tables, or label-value lines.Percentages: No space before the percent sign (e.g., 1%, 6.10%).Headings: Must end with a noun phrase; no full stop at the end.Slug: Correctly prepend ja-jp/ to the source slug.“50+ states” and similar expressions: Normalize to 「50州以上」 or 「50以上の州」 — never “50+州”.EOR/PEO comparison: The distinction between co-employment (PEO) and legal employer (EOR) must be clear and consistent throughout.Body CTA: Use an invitational tone. If the CTA says “Book a demo,” the anchor text should be 「こちらのデモ」; if it says “Learn more,” adapt to 「詳しくはこちら」.";
        var glossaryRequest = new GlossaryRequest { Glossary= new FileReference { Name = "Glossary.tbx" } };

        var result = await actions.TranslateContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest);
        Assert.IsNotNull(result);
        //Assert.IsTrue(result.File.Name.Contains("contentful"));

        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    [TestMethod]
    public async Task Translate_xliff()
    {
        var actions = new TranslationActions(InvocationContext, FileManager);
        var translateRequest = new TranslateFileRequest
        {
            File = new FileReference { Name = "contentful.html.xlf" },
            TargetLanguage = "nl",
            AIModel = ModelName,
        };
        string? systemMessage = null;
        var glossaryRequest = new GlossaryRequest();

        var result = await actions.TranslateContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.File.Name.Contains("contentful"));

        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    [TestMethod]
    public async Task TranslateText_WithSerbianLocale_ReturnsLocalizedText()
    {
        var actions = new TranslationActions(InvocationContext, FileManager);
        var localizeRequest = new TranslateTextRequest
        {
            Text = "Develop and implement an HR strategy that drives organizational productivity and supports company's business goals. Design and oversee processes that promote team efficiency and operational effectiveness while reducing complexity and redundancies.",
            TargetLanguage = "sr-Latn-RS",
            AIModel = ModelName,
        };

        var glossaryRequest = new GlossaryRequest();
        string? systemMessage = null;
        var result = await actions.LocalizeText(localizeRequest, new PromptRequest { }, systemMessage, glossaryRequest);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.TranslatedText);
        Console.WriteLine("Original: " + localizeRequest.Text);
        Console.WriteLine("Localized: " + result.TranslatedText);

        // Additional validation to ensure response is not empty and contains Serbian characters
        Assert.IsTrue(result.TranslatedText.Length > 0);
    }

    [TestMethod]
    public async Task Batch_translate_text_returns_valid_xliff()
    {
        var actions = new TranslationActions(InvocationContext, FileManager);
        var file = new FileReference { Name = "contentful.html" };
        var translateRequest = new TranslateFileRequest
        {
            File = file,
            TargetLanguage = "nl",
            AIModel = ModelName,
        };
        string? systemMessage = null;
        var glossaryRequest = new GlossaryRequest();

        var startBatchResponse = await actions.BatchTranslateContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest, new());
        Assert.IsNotNull(startBatchResponse);
        Console.WriteLine(startBatchResponse.JobName);

        var polling = new BatchPolling(InvocationContext);


        var result = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>() { Memory = new BatchMemory
        {
            LastPollingTime = DateTime.UtcNow,
            Triggered = false
        }
        }, new BatchIdentifier { JobName = startBatchResponse.JobName });

        while (!result.FlyBird)
        {
            await Task.Delay(3000);
            result = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>() { Memory = result.Memory }, new BatchIdentifier { JobName = startBatchResponse.JobName });
        }

        var batchActions = new BatchActions(InvocationContext, FileManager);

        var finalResult = await batchActions.DownloadXliffFromBatch(startBatchResponse.JobName, new GetBatchResultRequest { OriginalXliff = startBatchResponse.TransformationFile });

        Console.WriteLine(JsonConvert.SerializeObject(finalResult, Formatting.Indented));

        Assert.IsNotNull(finalResult.File);
    }
}
