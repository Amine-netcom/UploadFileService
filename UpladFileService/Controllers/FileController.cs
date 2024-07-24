using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpladFileService.Model;

namespace UpladFileService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;
        private readonly Config _config;

        public FileController(ILogger<FileController> logger, IOptions<Config> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile()
        {
            try
            {
                if (Request.ContentLength == 0)
                {
                    return NoContent();
                }
                
                LogInformation($"config max size: {_config.maximum_file_size_in_MB}");
                LogInformation($"config path: {_config.tmp_path}");
                LogInformation($"config validity: {_config.validity_period}");

                string uniqueFileName = $"{Guid.NewGuid()}_{DateTime.Now.Ticks}.tmp";
                LogInformation($"File name: {uniqueFileName}");

                string filePath = Path.Combine(_config.tmp_path, uniqueFileName);
              

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await Request.Body.CopyToAsync(stream);
                }
                
                var fileInfo = new FileInfo(filePath);
                long fileSizeInMB = fileInfo.Length / (1024 * 1024);
                
                if (fileSizeInMB > _config.maximum_file_size_in_MB)
                {
                    LogError($"Uploaded file size exceeds configured limit: {_config.maximum_file_size_in_MB} MB.");
                    System.IO.File.Delete(filePath);
                    return BadRequest($"Uploaded file size exceeds configured limit: {_config.maximum_file_size_in_MB} MB.");
                }
                
                string baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";
                string httpUrl = $"{baseUrl}/{uniqueFileName}";
                LogInformation($"Url: {httpUrl}");
                var xmlDoc = new System.Xml.XmlDocument();
                var fileNode = xmlDoc.CreateElement("file", "urn:gsma:params:xml:ns:rcs:rcs:fthttp");
                xmlDoc.AppendChild(fileNode);

                var fileInfoNode = xmlDoc.CreateElement("file-info");
                fileInfoNode.SetAttribute("type", "file");
                fileNode.AppendChild(fileInfoNode);

                var fileSizeNode = xmlDoc.CreateElement("file-size");
                fileSizeNode.InnerText = fileInfo.Length.ToString();
                fileInfoNode.AppendChild(fileSizeNode);

                var fileNameNode = xmlDoc.CreateElement("file-name");
                fileNameNode.InnerText = uniqueFileName;
                fileInfoNode.AppendChild(fileNameNode);

                var contentTypeNode = xmlDoc.CreateElement("content-type");
                contentTypeNode.InnerText = "application/octet-stream";
                fileInfoNode.AppendChild(contentTypeNode);

                var dataNode = xmlDoc.CreateElement("data");
                dataNode.SetAttribute("url", httpUrl);
                dataNode.SetAttribute("until", DateTime.UtcNow.AddSeconds(_config.validity_period).ToString("yyyy-MM-ddTHH:mm:ssZ"));
                fileInfoNode.AppendChild(dataNode);

                return Content(xmlDoc.OuterXml, "application/xml");
            }
            catch (Exception ex)
            {
                //LogError($"Error processing file upload: {ex.ToString()}");
                //return StatusCode(500, "Internal server error.");
                throw ex;
            }
        }

        private void LogError(string message)
        {
            _logger.LogError(message);
        }

        private void LogInformation(string message)
        {
            _logger.LogInformation(message);
        }
    }
}