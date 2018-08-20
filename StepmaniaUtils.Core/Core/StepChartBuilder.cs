﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using StepmaniaUtils.Enums;
using StepmaniaUtils.StepChart;

namespace StepmaniaUtils.Core
{
    public static class StepChartBuilder
    {
        public static bool GenerateLightsChart(SmFile file)
        {
            var reference = file.ChartMetadata.GetSteps(PlayStyle.Single, SongDifficulty.Hard)
                         ?? file.ChartMetadata.GetSteps(PlayStyle.Single, SongDifficulty.Challenge)
                         ?? file.ChartMetadata.GetSteps(PlayStyle.Double, SongDifficulty.Hard)
                         ?? file.ChartMetadata.GetSteps(PlayStyle.Double, SongDifficulty.Challenge)
                         ?? file.ChartMetadata.GetSteps(PlayStyle.Single, file.ChartMetadata.GetHighestChartedDifficulty(PlayStyle.Single))
                         ?? file.ChartMetadata.GetSteps(PlayStyle.Double, file.ChartMetadata.GetHighestChartedDifficulty(PlayStyle.Double));

            if (reference == null)
                throw new ArgumentException("Could not find a reference chart.", nameof(file));

            var rawChartData = GetRawChartData(file.FilePath, reference);

            return true;
        }

        private static string GetRawChartData(string file, StepMetadata steps)
        {
            //TODO: Try to do it with just one buffer, likely all that's needed.
            var tagBuffer = new StringBuilder();
            var valueBuffer = new StringBuilder();
            var dataBuffer = new StringBuilder();

            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    if (reader.Peek() == ':')
                    {
                        //buffer contains tag in the format #TAG
                        var tag = tagBuffer.SkipWhile(c => c != '#').ToString().Trim('#').ToAttribute();
                        
                        if (tag == SmFileAttribute.NOTES)
                        {
                            var stepData = SmFile.ReadStepchartMetadata(reader, valueBuffer);
                            if (stepData.PlayStyle == steps.PlayStyle && stepData.Difficulty == steps.Difficulty)
                            {
                                //Skip groove radar values
                                SmFile.ReadNextNoteHeaderSection(reader, valueBuffer);

                                while (reader.Peek() != ';') dataBuffer.Append((char) reader.Read());

                                return dataBuffer.ToString();
                            }

                            //wrong chart, skip the stream reader ahead to the next tag
                            while (reader.Peek() != ';') reader.Read();
                        }
                        else
                        {
                            //skip
                            while (reader.Peek() != ';') reader.Read();
                        }

                        tagBuffer.Clear();

                    }
                    else
                    {
                        tagBuffer.Append((char)reader.Read());
                    }
                }
            }

            return string.Empty;
        }

        public static StepData GenerateLightsChart(StepData referenceChart)
        {
            if (referenceChart == null)
                throw new ArgumentNullException(nameof(referenceChart), "referenceChart cannot be null.");

            if (!referenceChart.Measures.Any())
            {
                throw new ArgumentException("Reference chart does not contain Measure Data", nameof(referenceChart));
            }

            var lightChart = new StepData(PlayStyle.Lights, SongDifficulty.Easy, difficultyRating: 1, chartAuthor: "Auto-Generated");

            bool isHolding = false;
            foreach (var referenceMeasure in referenceChart.Measures)
            {
                int quarterNoteBeatIndicator = referenceMeasure.Notes.Count / 4;
                int noteIndex = 0;

                var newMeasure = new MeasureData();

                foreach (var note in referenceMeasure.Notes)
                {
                    string marqueeLights = note.Columns.Replace('M', '0'); //ignore mines

                    if (referenceChart.PlayStyle == PlayStyle.Double)
                    {
                        marqueeLights = MapMarqueeLightsForDoubles(marqueeLights);
                    }

                    bool isQuarterBeat = noteIndex % quarterNoteBeatIndicator == 0;
                    bool hasNote = marqueeLights.Any(c => c != '0');
                    bool isHoldBegin = marqueeLights.Any(c => c == '2' || c == '4');
                    bool isHoldEnd = marqueeLights.Any(c => c == '3');
                    bool isJump = marqueeLights.Count(c => c != '0') >= 2;

                    string bassLights = (hasNote && isQuarterBeat) || isJump ? "11" : "00";

                    if (isHoldBegin && !isHolding)
                    {
                        bassLights = "22"; //hold start
                        isHolding = true;
                    }
                    else if (isHolding)
                    {
                        bassLights = "00"; //ignore beats if there is a hold
                    }

                    if (isHoldEnd && !isHoldBegin)
                    {
                        bassLights = "33"; //hold end
                        isHolding = false;
                    }

                    var noteData = new ColumnData($"{marqueeLights}{bassLights}00");

                    newMeasure.Notes.Add(noteData);
                    noteIndex++;
                }

                lightChart.Measures.Add(newMeasure);
            }

            return lightChart;
        }

        private static string MapMarqueeLightsForDoubles(string marqueeLights)
        {
            string convertedMarqueeLights = string.Empty;
            for (int i = 0; i < 4; i++)
            {
                char note = '0';

                int p1 = i;
                int p2 = i + 4;

                if (marqueeLights[p1] != '0') note = marqueeLights[p1];
                if (marqueeLights[p2] != '0') note = marqueeLights[p2];

                convertedMarqueeLights += note;
            }

            return convertedMarqueeLights;
        }
    }
}