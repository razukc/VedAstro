﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace API
{
    public class VisitorAPI
    {
        [FunctionName("addvisitor")]
        public static async Task<IActionResult> AddVisitor(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage incomingRequest,
            [Blob(APITools.VisitorLogXml, FileAccess.ReadWrite)] BlobClient visitorLogClient)
        {
            try
            {
                //get new visitor data out of incoming request 
                var newVisitorXml = APITools.ExtractDataFromRequest(incomingRequest);

                //add new visitor to main list
                var taskListXml = await APITools.AddXElementToXDocument(visitorLogClient, newVisitorXml);

                //upload modified list to storage
                await APITools.OverwriteBlobData(visitorLogClient, taskListXml);

                return APITools.PassMessage();

            }
            catch (Exception e)
            {
                //log error
                await Log.Error(e, incomingRequest);

                //format error nicely to show user
                return APITools.FormatErrorReply(e);
            }

        }

        /// <summary>
        /// Gets all the unique visitors to the site
        /// </summary>
        [FunctionName("getvisitorlist")]
        public static async Task<IActionResult> GetVisitorList(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage incomingRequest,
            [Blob(APITools.VisitorLogXml, FileAccess.ReadWrite)] BlobClient visitorLogClient)
        {
            var responseMessage = "";

            try
            {

                //get user id
                var userId = APITools.ExtractDataFromRequest(incomingRequest).Value;

                //get visitor log from storage
                var visitorLogXml = await APITools.BlobClientToXml(visitorLogClient);

                //get all unique visitor elements only
                //var uniqueVisitorList = from visitorXml in visitorLogXml.Root?.Elements()
                //                        where
                //                            //note: location tag only exists for new visitor log,
                //                            //so use that to get unique list
                //                            visitorXml.Element("Location") != null
                //                        select visitorXml;

                //send list to caller
                responseMessage = visitorLogXml.ToString();

            }
            catch (Exception e)
            {
                //log error
                await Log.Error(e, incomingRequest);

                //format error nicely to show user
                return APITools.FormatErrorReply(e);
            }


            var okObjectResult = new OkObjectResult(responseMessage);

            return okObjectResult;
        }

        [FunctionName("deletevisitorbyuserid")]
        public static async Task<IActionResult> DeleteVisitorByUserId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage incomingRequest,
            [Blob(APITools.VisitorLogXml, FileAccess.ReadWrite)] BlobClient visitorLogClient)
        {

            try
            {
                //get unedited hash & updated person details from incoming request
                var userIdXml = APITools.ExtractDataFromRequest(incomingRequest);
                var userId = userIdXml.Value;

                //get all visitor elements that needs to be deleted
                var visitorListXml = await APITools.BlobClientToXml(visitorLogClient);
                var visitorLogsToDelete = visitorListXml.Root?.Elements().Where(x => x.Element("UserId")?.Value == userId).ToList();

                //delete each record
                foreach (var visitorXml in visitorLogsToDelete)
                {
                    visitorXml.Remove();
                }

                //upload modified list to storage
                await APITools.OverwriteBlobData(visitorLogClient, visitorListXml);

                return APITools.PassMessage();

            }
            catch (Exception e)
            {
                //log error
                await Log.Error(e, incomingRequest);
                //format error nicely to show user
                return APITools.FormatErrorReply(e);
            }

        }

        [FunctionName("deletevisitorbyvisitorid")]
        public static async Task<IActionResult> DeleteVisitorByVisitorId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage incomingRequest,
            [Blob(APITools.VisitorLogXml, FileAccess.ReadWrite)] BlobClient visitorLogClient)
        {

            try
            {
                //get unedited hash & updated person details from incoming request
                var visitorIdXml = APITools.ExtractDataFromRequest(incomingRequest);
                var visitorId = visitorIdXml.Value;

                //get all visitor elements that needs to be deleted
                var visitorListXml = await APITools.BlobClientToXml(visitorLogClient);
                var visitorLogsToDelete = (from xml in visitorListXml.Root?.Elements()
                                           where xml.Element("VisitorId")?.Value == visitorId
                                           select xml).ToList();

                //delete each record
                foreach (var visitorXml in visitorLogsToDelete)
                {
                    visitorXml.Remove();
                }

                //upload modified list to storage
                await APITools.OverwriteBlobData(visitorLogClient, visitorListXml);

                return APITools.PassMessage();

            }
            catch (Exception e)
            {
                //log error
                await Log.Error(e, incomingRequest);
                //format error nicely to show user
                return APITools.FormatErrorReply(e);
            }

        }


    }
}
