﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Genso.Astrology.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace API
{
    /// <summary>
    /// All API calls with no home are here, send them somewhere you think is good
    /// </summary>
    public class GeneralAPI
    {

        [FunctionName("getmatchreport")]
        public static async Task<IActionResult> GetMatchReport([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage incomingRequest)
        {

            try
            {
                //get name of male & female
                var rootXml = APITools.ExtractDataFromRequest(incomingRequest);
                var maleId = rootXml.Element("MaleId")?.Value;
                var femaleId = rootXml.Element("FemaleId")?.Value;

                //generate compatibility report
                var compatibilityReport = await APITools.GetCompatibilityReport(maleId, femaleId);
                return APITools.PassMessage(compatibilityReport.ToXml());
            }
            catch (Exception e)
            {
                //log error
                await ApiLogger.Error(e, incomingRequest);
                //format error nicely to show user
                return APITools.FailMessage(e);
            }

        }

        [FunctionName("gethoroscope")]
        public static async Task<IActionResult> GetHoroscope([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage incomingRequest)
        {

            try
            {
                //get person from request
                var rootXml = APITools.ExtractDataFromRequest(incomingRequest);
                var personId = rootXml.Value;

                var person = await APITools.GetPersonFromId(personId);

                //calculate predictions for current person
                var predictionList = await APITools.GetPrediction(person);

                var sortedList = SortPredictionData(predictionList);

                //convert list to xml string in root elm
                return APITools.PassMessage(Tools.AnyTypeToXmlList(sortedList));

            }
            catch (Exception e)
            {
                //log error
                await ApiLogger.Error(e, incomingRequest);
                //format error nicely to show user
                return APITools.FailMessage(e);
            }



            List<HoroscopePrediction> SortPredictionData(List<HoroscopePrediction> horoscopePredictions)
            {
                //put rising sign at top
                horoscopePredictions.MoveToBeginning((horPre) => horPre.FormattedName.ToLower().Contains("rising"));

                //todo followed by planet in sign prediction ordered by planet strength 

                return horoscopePredictions;
            }

        }



    }
}
