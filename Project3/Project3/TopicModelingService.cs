using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.ML; 
using Microsoft.ML.Data; 


public class TopicInput
{
    [LoadColumn(0)]
    public string Topic { get; set; }

    [LoadColumn(1)]
    public string Text { get; set; }
}

public class TopicPrediction
{
    public string PredictedTopic { get; set; }

    public float[] Score { get; set; }
}

public class TopicModelingService
{
    private readonly MLContext _mlContext;
    private readonly PredictionEngine<TopicInput, TopicPrediction> _predictionEngine;

    // fiksna lista vazecih tema
    private readonly HashSet<string> ValidTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "kriptovalute", "finansije", "tehnologija", "sport", "zdravlje", "politika" // SVA MALA SLOVA
    };

    public TopicModelingService()
    {
        _mlContext = new MLContext(seed: 0);

        var lines = new[]
        {
            "kriptovalute bitcoin crypto cryptocurrency btc ethereum blockchain",
            "finansije market business stock finance stocks earnings wallstreet trading investment deal merger acquisition buy low sell high",
            "tehnologija tech technology ai software programming code startup gadget",
            "sport sports game score football soccer tennis basketball nfl fantasy team league coach hof draft picks preseason regular season qb td championship",
            "zdravlje health medicine medical wellness fitness doctor",
            "politika government biden trump congress election bill law legislation vote senate"
        };

        // Svaka linija se deli na prvu rec (Tema) i ostatak reci (Tekst/Features).
        var parsedData = lines.Select(line =>
        {
            var parts = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            return new TopicInput { Topic = parts[0], Text = parts.Length > 1 ? parts[1] : string.Empty };
        }).ToList();

        var trainingData = _mlContext.Data.LoadFromEnumerable(parsedData);


        // Obrada podataka i trening
        var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                inputColumnName: nameof(TopicInput.Topic),
                outputColumnName: "Label")
            .Append(_mlContext.Transforms.Text.FeaturizeText(     // Stvara novu kolonu "Features" koja je numerički prikaz teksta
                inputColumnName: nameof(TopicInput.Text),
                outputColumnName: "Features"))
            .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(     // Trening modela
                labelColumnName: "Label",
                featureColumnName: "Features"))
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue(                       // vraca predvidjeni numericki kljuc
                inputColumnName: "PredictedLabel",
                outputColumnName: nameof(TopicPrediction.PredictedTopic)));

        var trainedModel = pipeline.Fit(trainingData);

        _predictionEngine = _mlContext.Model.CreatePredictionEngine<TopicInput, TopicPrediction>(trainedModel);
    }

    public List<string> AnalyzeTopics(List<string> titles)
    {
        var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);       //koristimo hashset da ne bi imali duplikate i ne pravimo razliku 
                                                                                    //izmedju velikih i malih slova

        foreach (var title in titles ?? Enumerable.Empty<string>())
        {
            var input = new TopicInput { Text = title };

            var prediction = _predictionEngine.Predict(input);

            string best = prediction.PredictedTopic;

            if (!string.IsNullOrEmpty(best) && ValidTopics.Contains(best))
                detected.Add(best);
        }

        return detected.ToList();
    }
}