﻿using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Text.Json;
using System.Xml.Linq;
using VedAstro.Library;
using VedAstro.Library.Compatibility;

namespace API
{
    /// <summary>
    /// API with match related stuff
    /// </summary>
    public class MatchAPI
    {
        //PUBLIC API

        [Function(nameof(Match))]
        public static async Task<HttpResponseData> Match([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData incomingRequest)
        {
            try
            {
                //get data out of call
                var rootJson = await APITools.ExtractDataFromRequestJson(incomingRequest);

                //api key to ID the call
                var apiKey = rootJson.GetProperty("APIKey").ToString(); //treated here as owner ID

                //get person list
                var personListArray = rootJson.GetProperty("PersonList");

                //convert json list to parsed person list
                var ownerId = apiKey; //every person needs owner
                var personList = await JsonPersonListToPerson(personListArray, ownerId);

                //get 1st and 2nd only for now (todo support more)
                var male = personList[0];
                var female = personList[1];

                //generate compatibility report
                var compatibilityReport = MatchCalculator.GetCompatibilityReport(male, female);
                return APITools.PassMessageJson((XElement)compatibilityReport.ToXml(), incomingRequest);
            }
            catch (Exception e)
            {
                //log error
                await APILogger.Error(e, incomingRequest);
                //format error nicely to show user
                return APITools.FailMessageJson(e, incomingRequest);
            }
        }


        [Function("getmatchreport")]
        public static async Task<HttpResponseData> GetMatchReport([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData incomingRequest)
        {
            try
            {
                //get name of male & female
                var rootXml = await APITools.ExtractDataFromRequestXml(incomingRequest);
                var maleId = rootXml.Element("MaleId")?.Value;
                var femaleId = rootXml.Element("FemaleId")?.Value;

                //generate compatibility report
                var compatibilityReport = await GetCompatibilityReport(maleId, femaleId);
                return APITools.PassMessage((XElement)compatibilityReport.ToXml(), incomingRequest);
            }
            catch (Exception e)
            {
                //log error
                await APILogger.Error(e, incomingRequest);
                //format error nicely to show user
                return APITools.FailMessage(e, incomingRequest);
            }
        }

        [Function("GetAllMatchForPerson")]
        public static async Task<HttpResponseData> GetAllMatchForPerson([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequestData incomingRequest, string personId)
        {
            //todo needs work person ID  is not being fed in

            var person = await APITools.GetPersonById(personId);

            var personList = await GetAllPersonByMatchStrength(person);

            var text = Tools.ListToString(personList);

            return APITools.PassMessage(text, incomingRequest);
        }



        //PRIVATE

        private const string dataPersonlistXml = "data\\PersonList.xml";

        private static async Task<List<Person>> JsonPersonListToPerson(JsonElement personListArray, string apiKey)
        {
            var returnList = new List<Person>();
            var personList = personListArray.EnumerateArray();

            while (personList.MoveNext())
            {
                //NOTE: the person data received here is akin to user entering it via simple editor (so keep it human ready)

                //1: extract the data out
                var personJson = personList.Current;
                var name = personJson.GetProperty("Name").ToString();
                var genderText = personJson.GetProperty("Gender").ToString();
                var birthTimeJson = personJson.GetProperty("BirthTime");

                //we're inside birth time 
                var stdTime = birthTimeJson.GetProperty("StdTime").ToString();
                var locationName = birthTimeJson.GetProperty("LocationName").ToString();

                //2: make new person

                //parse data received from user
                var results = await Tools.AddressToGeoLocation(locationName);
                if (!results.IsPass) { throw new Exception($"Failed to get location {locationName}"); }
                var geoLocation = results.Payload;

                //var birthTime = await _timeInput.GetTime(geoLocation);
                var birthTime = new Time(stdTime, geoLocation);

                //each person profile must have a owner, here the unique API key
                var ownerId = apiKey;

                //there is possibility user has put invalid characters, clean it!
                var nameInput = Tools.CleanNameText(name);

                //new person ID made from thin air
                var newPersonProfileId = Tools.GenerateId();

                //get gender from gender string
                var gender = Enum.Parse<Gender>(genderText);

                //create the new complete person profile
                var newPerson = new Person(newPersonProfileId, nameInput, birthTime, gender, new[] { ownerId });

                //add to list
                returnList.Add(newPerson);
            }

            return returnList;
        }

        public static async Task<CompatibilityReport> GetCompatibilityReport(string maleId, string femaleId)
        {
            var male = await APITools.GetPersonById(maleId);
            var female = await APITools.GetPersonById(femaleId);

            //if male & female profile found, make report and return caller
            var notEmpty = !Person.Empty.Equals(male) && !Person.Empty.Equals(female);
            if (notEmpty)
            {
                return MatchCalculator.GetCompatibilityReport(male, female);
            }
            else
            {
                throw new Exception(AlertText.PersonProfileNoExist);
            }
        }

        private static async Task PrintOneVsList(Person person)
        {
            //get all the people
            var peopleList = await APITools.GetAllPersonList();

            //given a list of people find good matches
            //var goodMatches = FindGoodMatches(peopleList);
            var goodMatches = GetAllMatchesForPersonByStrength(person, peopleList);

            //show final results to user
            printResultList(ref goodMatches);

            void printResultList(ref List<CompatibilityReport> reportList)
            {
                foreach (var report in reportList)
                {
                    Console.WriteLine($"{report.Male.Name}\t{report.Female.Name}\t{report.KutaScore}");
                }

                Console.ReadLine();
            }
        }

        private static List<CompatibilityReport> GetAllMatchesForPersonByStrength(Person inputPerson, List<Person> personList)
        {
            var returnList = new List<CompatibilityReport>();

            //this makes sure each person is cross checked against this person correctly
            foreach (var personMatch in personList)
            {
                //get needed details
                var inputPersonIsMale = inputPerson.Gender == Gender.Male;
                var inputPersonIsFemale = inputPerson.Gender == Gender.Female;
                var personMatchIsMale = personMatch.Gender == Gender.Male;
                var personMatchIsFemale = personMatch.Gender == Gender.Female;

                if (inputPersonIsMale && personMatchIsFemale)
                {
                    //add report to list
                    var report = MatchCalculator.GetCompatibilityReport(inputPerson, personMatch);
                    returnList.Add(report);
                }

                if (inputPersonIsFemale && personMatchIsMale)
                {
                    //add report to list
                    var report = MatchCalculator.GetCompatibilityReport(personMatch, inputPerson);
                    returnList.Add(report);
                }
            }

            //order the list by strength, highest at 0 index
            var SortedList = returnList.OrderBy(o => o.KutaScore).ToList();

            return SortedList;
        }

        private static async Task<List<Person>> GetAllPersonByMatchStrength(Person inputPerson)
        {
            var resultList = new List<CompatibilityReport>();

            var inputPersonIsMale = inputPerson.Gender == Gender.Male;

            var personList = await APITools.GetAllPersonList();

            //this makes sure each person is cross checked against this person correctly
            foreach (var personMatch in personList)
            {
                //add report to list
                CompatibilityReport report;

                //sex orientation depends on input person only
                //in other words input person is always placed in correct sex calculator
                //note : done so that same sex can be checked without to much code
                //       & male can be checked from female position
                if (inputPersonIsMale)
                {
                    report = MatchCalculator.GetCompatibilityReport(inputPerson, personMatch);
                }
                //input person is female
                else
                {
                    report = MatchCalculator.GetCompatibilityReport(personMatch, inputPerson);
                }

                resultList.Add(report);
            }

            //order the list by strength, highest at 0 index
            var resultListOrdered = resultList.OrderBy(o => o.KutaScore).ToList();

            //get needed details

            List<Person> personList2;
            if (inputPersonIsMale) { personList2 = resultListOrdered.Select(x => x.Female).ToList(); }
            else { personList2 = resultListOrdered.Select(x => x.Male).ToList(); }

            return personList2;
        }

        /// <summary>
        /// Finds good matches from a list of people who meet the criteria
        /// </summary>
        private static List<CompatibilityReport> FindGoodMatches(List<Person> peopleList)
        {
            //from a list of people find good matches

            //split the sexes
            var femaleList = peopleList.FindAll(person => person.Gender == Gender.Female);
            var maleList = peopleList.FindAll(person => person.Gender == Gender.Male);

            var goodReports = new List<CompatibilityReport>();

            //cross reference male & female list
            foreach (var female in femaleList)
            {
                foreach (var male in maleList)
                {
                    var report = MatchCalculator.GetCompatibilityReport(male, female);
                    //if report meets criteria save it
                    if (report.KutaScore > 50)
                    {
                        goodReports.Add(report);
                    }
                }
            }

            //return reports that got saved
            return goodReports;
        }
    }
}