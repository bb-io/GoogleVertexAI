using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.DataSourceHandlers;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Dynamic;
using GoogleVertexAI.Base;

namespace Tests.GoogleVertexAI
{
    [TestClass]
    public class DataSources : TestBase
    {
        [TestMethod]
        public async Task XLIFFScoreReturnsValues()
        {

            var action = new GeminiActions(InvocationContext,FileManager);

            var input = 0;

            var input1 = new ScoreXliffRequest 
            {
                File = new Blackbird.Applications.Sdk.Common.Files.FileReference {Name= "translated_anthropic.xliff" },
                //SourceLanguage = "en_GB",
                //TargetLanguage = "de_DE",
                
            };
            string input2 = "[INSTRUCTIONS]First, determine the overarching domain, field or topic of the entire set of sentences considered as a whole.\r\nNext, assess the style, accuracy and appropriateness of the translation within the specific domain, field or topic identified in the previous step.\r\n\r\nSpecifically, assign a score of '0' if:\r\n\r\n- the translation contains a term, keyword or phrase that should be rendered differently for the identified domain, field or topic\r\n- the source content is contextually undetermined, such as single terms, keywords or phrases present in the source text lack sufficient context for the accuracy of their translation to be determined\r\n- the translation contains a phrase or formulation that sounds unnatural or awkward (to assess this, consider whether the phrase or formulation is commonly used by native writers, taking into account idiomatic expressions and contextually appropriate language)\r\n- the translation is hard to understand, such as a competent reader would have difficulty to comprehend the meaning of the translation upon first reading. For example, if the translation contains overly embedded clauses that would be due to an inappropriate retention of the structure of the source text\r\n- the translation introduces a potential semantic ambiguity that is not present in the source text- the tone or formality level of the translation is not consistent with the other translations of the current dataset input (only flag as inconsistent the sentences which are outliers)\r\n- the translation does not accurately represent the propositional content of the source text\r\n[/INSTRUCTIONS]\r\n\r\n[EXAMPLES-WITH-ENGLISH-to-FRENCH-TRANSLATIONS]\r\nSource: To better understand your target market and customer demographics, here are a few questions you can ask.\r\nTranslation: Pour mieux comprendre votre marché cible et les caractéristiques démographiques de vos clients, voici quelques questions que vous pouvez poser.\r\nScore = 10\r\n\r\nSource: Hopefully everyone has a break over Christmas/New Year and is fully recharged for everything we have discussed in 2024\r\nTranslation: Espérons que tout le monde fasse une pause à Noël et au Nouvel An et soit complètement rechargé pour tout ce dont nous avons discuté en 2024\r\nScore = 0 \r\nRationale = unnatural translation (\"soit complètement rechargé\" is a literal translation that does not correspond to any existing idiom in French)\r\n\r\nSource: How do you like to make purchases?\r\nTranslation: Comment aimez-vous faire vos achats ?\r\nScore = 10\r\n\r\nSource: I would like to reach out regarding the 2024 Holiday Period.\r\nTranslation: J'aimerais vous contacter concernant la période des fêtes 2024.\r\nScore = 10\r\n\r\nSource: Dependent Categories.\r\nTranslation: Catégories dépendantes.\r\nScore = 0\r\nRationale = source contextually undetermined\r\n\r\nSource:  They are expecting from us transparency.\r\nTranslation: Ils s'attendent à ce que l'on soit transparent.\r\nScore = 10\r\n\r\nSource: So, we really see the difference.\r\nTranslation: Donc, nous voyons vraiment la différence.\r\nScore = 0\r\nRationale = awkward formulation, inappropriate retention of the source structure (\"Donc, \")\r\n\r\nSource: A broad spectrum of marketing experience\r\nTranslation: Un large éventail d’expérience dans le domaine du marketing\r\nScore = 0\r\nRationale = unnatural translation (\"Un large éventail d’expérience\" is a literal translation that does not correspond to any existing idiom in French)\r\n\r\nSource: I hope you are doing well.\r\nTranslation: J'espère que tu vas bien.\r\nScore = 0\r\nRationale = formality level not consistent with the other translations of the current dataset input\r\n\r\nSource: We will grant time off based on an annual rotation system.\r\nTranslation: Nous accorderons du temps libre selon un système de rotation annuelle..\r\nScore = 0\r\nRationale = a term or expression does not match the identified domain, field or topic (\"accorderons du temps libre\" should be translated as \"approuverons les demandes de congés\")\r\n\r\nSource: Nothing we do is worth getting hurt for.\r\nTranslation: Rien de ce que nous faisons ne mérite une blessure.\r\nScore = 0\r\nRationale = source contextually undetermined and unnatural translation (\"ne mérite une blessure\" is a literal translation that does not correspond to any existing idiom in French)\r\n\r\nSource: The unique features of the mining industry require tailor-made policies\r\nTranslation: Les caractéristiques propres à l'industrie minière nécessitent des politiques sur mesure\r\nScore = 10\r\n\r\nSource: Poor\r\nTranslation: Pauvre\r\nScore = 0\r\nRationale = source contextually undetermined\r\n\r\nSource: ON\r\nTranslation: ACTIVÉ\r\nScore = 0\r\nRationale = source contextually undetermined\r\n\r\n[/EXAMPLES-WITH-ENGLISH-to-FRENCH-TRANSLATIONS]";

            //string input2 = "";

            //var input3 = new PromptRequest { ModelEndpoint= "projects/eighth-effect-414910/locations/us-west1/publishers/google/models/gemini-1.5-flash" };
            var input3 = new PromptRequest { ModelEndpoint= "gemini-1.5-flash" };


            var result = await action.ScoreXLIFF(input1,input2, input3);
            Console.WriteLine(result.AverageScore);
            Assert.IsNotNull(result);
        }


        [TestMethod]
        public async Task EndpointDataReturnsValues()
        {

            var action = new EndpointDataSourceHandler(InvocationContext);

            var result = await action.GetDataAsync(new DataSourceContext { SearchString=""}, CancellationToken.None);

            foreach (var item in result) 
            {
                Console.WriteLine($"{item.Key}-{item.Value}");
                Assert.IsNotNull(item);
            }

            Assert.IsNotNull(result);
        }

    }
}
