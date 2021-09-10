using _999Space.BAL.ML.MLInterface;
using _999Space.Common.Enum;
using _999Space.Common.ViewModels;
using _999Space.DAL.DataModels;
using _999Space.DAL.Interfaces;
using _999Space.DAL.Repository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace _999Space.BAL.ML.MLService
{
    public class ModelBuilderService : IModelBuilder
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private static MLContext mlContext = new MLContext(seed: 1);
        private readonly ILogger<ModelBuilderService> _logger;
        public ModelBuilderService(IWebHostEnvironment env, IConfiguration configuration, ILogger<ModelBuilderService> logger)
        {
            _env = env;
            _configuration = configuration;
            _logger = logger;
        }
        public void CreateModel()
        {
            string connectionString = _configuration["ConnectionStrings:MLConnection"];
            string commandText = "SELECT [SearchText], [Category], [SubCategory], [Type], [Location], Cast(Isnull([PriceLimit], 0.0) as REAL) AS [PriceLimit]," +
                                 "[Mode],[Currency], [PriceType], Cast(Isnull([Frequency],0) as REAL) AS [Frequency], [Repeat], [Availability], [Tags], " +
                                 "[Gender],[Language],[Race],[Skills] FROM [dbo].[SearchTextData]";
            DatabaseLoader loader = mlContext.Data.CreateDatabaseLoader<SearchTextDataVM>();
            DatabaseSource dbSource = new DatabaseSource(SqlClientFactory.Instance, connectionString, commandText);
            IDataView trainingDataView = loader.Load(dbSource);


            try
            {
                var dbContext = new _999SpaceContext();
                using IUnityOfWork unityOfWork = new UnityOfWork(dbContext);
                var mlModelPath = unityOfWork.CommonRepository.GetMlModelPath();
                if (mlModelPath != null)
                {
                    string path = _env.WebRootPath;
                    if (mlModelPath.ModelSelectedPath.Equals((int)MLModelPath.ModelPath1))
                    {
                        path += mlModelPath.ModelPath2;// update models in opposite folder then make that folder selected
                    }
                    else
                    {
                        path += mlModelPath.ModelPath1;
                    }

                    //-- create both directory if not exist
                    if (!Directory.Exists(_env.WebRootPath + "\\" + mlModelPath.ModelPath1))
                    {
                        Directory.CreateDirectory(_env.WebRootPath + "\\" + mlModelPath.ModelPath1);
                    }
                    if (!Directory.Exists(_env.WebRootPath + "\\" + mlModelPath.ModelPath2))
                    {
                        Directory.CreateDirectory(_env.WebRootPath + "\\" + mlModelPath.ModelPath2);
                    }

                    //--- create model of each column using enum 
                    foreach (string predictedColumn in Enum.GetNames(typeof(PerdictedColumns)))
                    {
                        if (predictedColumn.Equals("Skills"))
                        {
                            // Build training pipeline
                            IEstimator<ITransformer> trainingPipeline = BuildTrainingPipeline(mlContext, predictedColumn.ToString());

                            // Train Model
                            ITransformer mlModel = TrainModel(mlContext, trainingDataView, trainingPipeline);

                            // Evaluate quality of Model
                            Evaluate(mlContext, trainingDataView, trainingPipeline, predictedColumn.ToString());

                            // Save model
                            SaveModel(mlContext, mlModel, $"{path}\\{predictedColumn}.zip", trainingDataView.Schema);
                        }

                    }

                    //--update ML model selected path 
                    if (mlModelPath.ModelSelectedPath.Equals((int)MLModelPath.ModelPath1))
                    {
                        mlModelPath.ModelSelectedPath = (int)MLModelPath.ModelPath2;
                    }
                    else
                    {
                        mlModelPath.ModelSelectedPath = (int)MLModelPath.ModelPath1;
                    }
                    unityOfWork.Save();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }

        }


        private IEstimator<ITransformer> BuildTrainingPipeline(MLContext mlContext, string predictColumn)
        {
            // Data process configuration with pipeline data transformations 
            var dataProcessPipeline = mlContext.Transforms.Conversion.MapValueToKey(predictColumn, predictColumn)
                                      .Append(mlContext.Transforms.Text.FeaturizeText("SearchText_tf", "SearchText"))
                                      .Append(mlContext.Transforms.CopyColumns("Features", "SearchText_tf"))
                                      .Append(mlContext.Transforms.NormalizeMinMax("Features", "Features"))
                                      .AppendCacheCheckpoint(mlContext);
            //// Set the training algorithm 
            var trainer = mlContext.MulticlassClassification.Trainers.OneVersusAll(mlContext.BinaryClassification.Trainers.AveragedPerceptron(labelColumnName: predictColumn, numberOfIterations: 10, featureColumnName: "Features"), labelColumnName: predictColumn)
                                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

            var trainingPipeline = dataProcessPipeline.Append(trainer);

            return trainingPipeline;
        }

        private ITransformer TrainModel(MLContext mlContext, IDataView trainingDataView, IEstimator<ITransformer> trainingPipeline)
        {
            ITransformer model = trainingPipeline.Fit(trainingDataView);

            return model;
        }

        private void Evaluate(MLContext mlContext, IDataView trainingDataView, IEstimator<ITransformer> trainingPipeline, string predictColumn)
        {
            // Cross-Validate with single dataset (since we don't have two datasets, one for training and for evaluate)
            // in order to evaluate and get the model's accuracy metrics         
            var crossValidationResults = mlContext.MulticlassClassification.CrossValidate(trainingDataView, trainingPipeline, numberOfFolds: 5, labelColumnName: predictColumn);
            PrintMulticlassClassificationFoldsAverageMetrics(crossValidationResults);
        }

        private void SaveModel(MLContext mlContext, ITransformer mlModel, string modelRelativePath, DataViewSchema modelInputSchema)
        {
            mlContext.Model.Save(mlModel, modelInputSchema, modelRelativePath);
        }

        private void PrintMulticlassClassificationFoldsAverageMetrics(IEnumerable<TrainCatalogBase.CrossValidationResult<MulticlassClassificationMetrics>> crossValResults)
        {
            var metricsInMultipleFolds = crossValResults.Select(r => r.Metrics);

            var microAccuracyValues = metricsInMultipleFolds.Select(m => m.MicroAccuracy);
            var microAccuracyAverage = microAccuracyValues.Average();
            var microAccuraciesStdDeviation = CalculateStandardDeviation(microAccuracyValues);
            var microAccuraciesConfidenceInterval95 = CalculateConfidenceInterval95(microAccuracyValues);

            var macroAccuracyValues = metricsInMultipleFolds.Select(m => m.MacroAccuracy);
            var macroAccuracyAverage = macroAccuracyValues.Average();
            var macroAccuraciesStdDeviation = CalculateStandardDeviation(macroAccuracyValues);
            var macroAccuraciesConfidenceInterval95 = CalculateConfidenceInterval95(macroAccuracyValues);

            var logLossValues = metricsInMultipleFolds.Select(m => m.LogLoss);
            var logLossAverage = logLossValues.Average();
            var logLossStdDeviation = CalculateStandardDeviation(logLossValues);
            var logLossConfidenceInterval95 = CalculateConfidenceInterval95(logLossValues);

            var logLossReductionValues = metricsInMultipleFolds.Select(m => m.LogLossReduction);
            var logLossReductionAverage = logLossReductionValues.Average();
            var logLossReductionStdDeviation = CalculateStandardDeviation(logLossReductionValues);
            var logLossReductionConfidenceInterval95 = CalculateConfidenceInterval95(logLossReductionValues);
        }

        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            double average = values.Average();
            double sumOfSquaresOfDifferences = values.Select(val => (val - average) * (val - average)).Sum();
            double standardDeviation = Math.Sqrt(sumOfSquaresOfDifferences / (values.Count() - 1));
            return standardDeviation;
        }

        private double CalculateConfidenceInterval95(IEnumerable<double> values)
        {
            double confidenceInterval95 = 1.96 * CalculateStandardDeviation(values) / Math.Sqrt((values.Count() - 1));
            return confidenceInterval95;
        }

    }
}
