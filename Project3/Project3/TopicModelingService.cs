using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;          // <-- for Encoding.UTF8
using SharpEntropy;
using SharpEntropy.IO;

//Topic modeling grupiše slične tekstove i dodeljuje im oznake (teme),
//bez da mu ti eksplicitno kažeš koja vest je o čemu — on to uči na osnovu reči koje se pojavljuju
//SharpEntropy je alat koji omogućava da naučiš model i da kasnije koristiš taj model za predviđanje (klasifikaciju)
//posmatra u ovom slučaju reč(i) koje se pojavljuju u naslovu i koristi te podatke da predvidi koja je najverovatnija klasa (tema) za taj tekst

public class TopicModelingService
{
    private GisModel _model;

    public TopicModelingService()
    {
        //podaci za treniranje, gde su prve reci teme, a ostale reci su vezane za tu temu
        var lines = new[]
        {
            "Kriptovalute bitcoin crypto cryptocurrency btc ethereum blockchain",
            "Finansije market business stock finance stocks earnings wallstreet",
            "Tehnologija tech technology ai software programming code startup gadget",
            "Sport sports game score football soccer tennis basketball",
            "Zdravlje health medicine medical wellness fitness doctor"
        };

        var text = string.Join(Environment.NewLine, lines);     //pravimo tekstualni sadrzaj 
        var bytes = Encoding.UTF8.GetBytes(text);       //pretvara u niz bajtova

        using (var ms = new MemoryStream(bytes))        //otvori kao virtuelni fajl u memoriji
        using (var sr = new StreamReader(ms, Encoding.UTF8, true))      //sharpentropy cita kao da je fajl
        {
            ITrainingEventReader events = new BasicEventReader(new PlainTextByLineDataReader(sr));  //ovo cita svaku liniju i pretvara u event(dogadjaj za ucenje)

            var trainer = new GisTrainer();     //pravimo trenera koji ce da trenira podatke
            trainer.TrainModel(events);     //treniramo model, model uci na osnovu podataka koje smo zadali

            _model = new GisModel(trainer);     //pravimo novi model koji je sada naucen
        }
    }

    public List<string> AnalyzeTopics(List<string> titles)
    {
        var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);       //koristimo hashset da ne bi imali duplikate i ne pravimo razliku 
                                                                                    //izmedju velikih i malih slova

        foreach (var title in titles ?? Enumerable.Empty<string>())
        {
            var ctx = Tokenize(title);
            var probs = _model.Evaluate(ctx);       //vraca procenu za svaku temu 
            if (probs == null || probs.Length == 0) continue;       //ako ne postoji ili je prazan string, preskoci naslov i predji na sledeci

            //pronalazi najverovatniju temu
            int bestIdx = 0;
            for (int i = 1; i < probs.Length; i++)
                if (probs[i] > probs[bestIdx]) bestIdx = i;

            string best = _model.GetBestOutcome(probs);     //vraca naziv teme 
            if (!string.IsNullOrEmpty(best)) detected.Add(best);
        }

        return detected.ToList();
    }

    //ovo koristimo kako bi iz teksta izdvojili samo reci tako da one budu sa svim malim slovima i tako da ignorisu sve znakove
    //kako bi to odgovaralo modelu za treniranje
    private static string[] Tokenize(string text)
    {
        return (text ?? string.Empty)
            .ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '-', '!', '?', ':', ';', '"', '\'', '(', ')', '/' },StringSplitOptions.RemoveEmptyEntries);
    }
}