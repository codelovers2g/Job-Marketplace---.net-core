using _999Space.BAL.ML.MLInterface;
using _999Space.Common.Enum;
using _999Space.Common.ViewModels;
using _999Space.DAL.DataModels;
using _999Space.DAL.Interfaces;
using _999Space.DAL.Repository;
using _999Space.Utility.Extensions.StringHelper;
using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using StopWord;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace _999Space.BAL.ML.MLService
{
    public class ConsumeModelService : IConsumeModel
    {
        private readonly IWebHostEnvironment _env;

        private readonly TextAnalyticsClient _client;
        private readonly IConfiguration _configuration;
        public ConsumeModelService(IWebHostEnvironment env, IConfiguration configuration)
        {
            _env = env;
            _configuration = configuration;
            _client = new TextAnalyticsClient(new Uri(_configuration["Cognitive:URL"]), new AzureKeyCredential(_configuration["Cognitive:Key"]));
        }

        public int Compute(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // Step 1  Verify arguments.
            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            // Step 2 Initialize arrays.
            for (int i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (int j = 0; j <= m; d[0, j] = j++)
            {
            }

            // Step 3 Begin looping.
            for (int i = 1; i <= n; i++)
            {
                //Step 4
                for (int j = 1; j <= m; j++)
                {
                    // Step 5 Compute cost.
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    // Step 6
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            // Step 7 Return cost.
            return d[n, m];
        }

        public CategorizedEntityCollection EntityRecognition(string text)
        {
            return _client.RecognizeEntities(text);
        }

        public TextTokens GetKeysInString(string text)
        {
            TextTokens textTokens = new TextTokens();

            if (!string.IsNullOrWhiteSpace(text))
                text = text.ToLower().Stopwords();

            var context = new MLContext();
            var emptyData = new List<TextData>();
            var data = context.Data.LoadFromEnumerable(emptyData);
            var tokenization = context.Transforms.Text.TokenizeIntoWords("Tokens", "Text", separators: new[] { ' ', ',', '.' })
                                                      .Append(context.Transforms.Text.RemoveDefaultStopWords("Tokens", "Tokens",
                                                            Microsoft.ML.Transforms.Text.StopWordsRemovingEstimator.Language.English));
            var tokenModel = tokenization.Fit(data);
            var engine = context.Model.CreatePredictionEngine<TextData, TextTokens>(tokenModel);
            textTokens = engine.Predict(new TextData { Text = text });

            return textTokens;
        }

        public T PerdictGenric<T>(ModelInput input, PerdictedColumns perdictedColumns) where T : class, new()
        {
            var dbContext = new _999SpaceContext();
            using (IUnityOfWork unityOfWork = new UnityOfWork(dbContext))
            {
                var mlModelPath = unityOfWork.CommonRepository.GetMlModelPath();
                string path = _env.WebRootPath;
                if (mlModelPath != null)
                {
                    
                    if (mlModelPath.ModelSelectedPath.Equals((int)MLModelPath.ModelPath1))
                    {
                        path += mlModelPath.ModelPath1;
                    }
                    else
                    {
                        path += mlModelPath.ModelPath2;
                    }

                }

                //----
                Lazy<PredictionEngine<ModelInput, T>> PredictionEngine = new Lazy<PredictionEngine<ModelInput, T>>(CreatePredictionEngineGenric<T>($"{path}\\{perdictedColumns}.zip"));
                T result = PredictionEngine.Value.Predict(input);
                return result;
            }


        }


        private PredictionEngine<ModelInput, T> CreatePredictionEngineGenric<T>(string Path) where T : class, new()
        {
            // Create new MLContext
            MLContext mlContext = new MLContext();

            // Load model & create prediction engine
            ITransformer mlModel = mlContext.Model.Load(Path, out var modelInputSchema);
            var predEngine = mlContext.Model.CreatePredictionEngine<ModelInput, T>(mlModel);
            return predEngine;
        }
    }
}
