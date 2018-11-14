// Copyright(c) 2018 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy of
// the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations under
// the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using Google.Cloud.BigQuery.V2;
using Google.Cloud.Storage.V1;
using Google.Apis.Storage.v1.Data;
using GoogleCloudSamples;
using Xunit;
using System.Text.RegularExpressions;
using System.Linq;

public class BigQueryTest : IDisposable
{
    private readonly string _projectId;
    private readonly BigQueryClient _client;
    List<BigQueryDataset> tempDatasets = new List<BigQueryDataset>();
    readonly StorageClient _storage;
    readonly string _bucketName;
    TextWriter consoleOut = Console.Out;
    StringWriter stringOut;

    public BigQueryTest()
    {
        _projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
        _client = BigQueryClient.Create(_projectId);
        _storage = StorageClient.Create();
        _bucketName = TestUtil.RandomName();
        _storage.CreateBucket(_projectId, _bucketName);
        stringOut = new StringWriter();
        Console.SetOut(stringOut);
    }

    [Fact]
    public void TestBrowseTable()
    {
        var snippet = new BigQueryBrowseTable();
        snippet.BrowseTable(_projectId);
        var outputLines = stringOut.ToString().Trim().Split(new [] { '\n' });
        Assert.Equal(10, outputLines.Count());
    }

    [Fact]
    public void TestCopyTable()
    {
        var snippet = new BigQueryCopyTable();
        string destinationDatasetId = CreateTempDataset();
        BigQueryTable table = snippet.CopyTable(
            _projectId, destinationDatasetId);
        Assert.True(table.Resource.NumRows > 0);
    }

    [Fact]
    public void TestCreateDataset()
    {
        var snippet = new BigQueryCreateDataset();
        BigQueryDataset dataset = snippet.CreateDataset(_projectId);
        Assert.Equal("US", dataset.Resource.Location);
        dataset.Delete();
    }

    [Fact]
    public void TestCreateTable()
    {
        var snippet = new BigQueryCreateTable();
        string datasetId = CreateTempDataset();
        BigQueryTable table = snippet.CreateTable(_projectId, datasetId);
        Assert.Equal("your_table_id", table.Resource.TableReference.TableId);
    }

    [Fact]
    public void TestDeleteDataset()
    {
        var snippet = new BigQueryDeleteDataset();
        string datasetId1 = TestUtil.RandomName();
        _client.CreateDataset(datasetId1);
        string datasetId2 = TestUtil.RandomName();
        _client.CreateDataset(datasetId2);
        CreateTempEmptyTable(datasetId2);
        snippet.DeleteDataset(_projectId, datasetId1, datasetId2);
        var output = stringOut.ToString();
        Assert.Contains($"{datasetId1} deleted", output);
        Assert.Contains($"{datasetId2} deleted", output);
    }

    [Fact]
    public void TestDeleteTable()
    {
        var snippet = new BigQueryDeleteTable();
        string datasetId = CreateTempDataset();
        string tableId = TestUtil.RandomName();
        _client.CreateTable(datasetId, tableId, null);
        snippet.DeleteTable(_projectId, datasetId, tableId);
        var output = stringOut.ToString();
        Assert.Contains($"{tableId} deleted", output);
    }

    [Fact]
    public void TestExtractTable()
    {
        var snippet = new BigQueryExtractTable();
        snippet.ExtractTable(_projectId, _bucketName);
        var uploadedFile = _storage.GetObject(_bucketName, "shakespeare.csv");
        Assert.True(uploadedFile.Size > 0);
    }

    [Fact]
    public void TestExtractTableJson()
    {
        var snippet = new BigQueryExtractTableJson();
        snippet.ExtractTableJson(_projectId, _bucketName);
        var uploadedFile = _storage.GetObject(_bucketName, "shakespeare.json");
        Assert.True(uploadedFile.Size > 0);
    }

    [Fact]
    public void TestListDatasets()
    {
        var snippet = new BigQueryListDatasets();
        var datasetId1 = CreateTempDataset();
        var datasetId2 = CreateTempDataset();
        snippet.ListDatasets(_projectId);
        var output = stringOut.ToString();
        Assert.Contains($"{datasetId1}", output);
        Assert.Contains($"{datasetId2}", output);
    }

    [Fact]
    public void TestListTables()
    {
        var snippet = new BigQueryListTables();
        var datasetId = CreateTempDataset();
        var tableId1 = CreateTempEmptyTable(datasetId);
        var tableId2 = CreateTempEmptyTable(datasetId);
        snippet.ListTables(_projectId, datasetId);
        var output = stringOut.ToString();
        Assert.Contains($"{tableId1}", output);
        Assert.Contains($"{tableId2}", output);
    }

    [Fact]
    public void TestLoadFromFile()
    {
        var snippet = new BigQueryLoadFromFile();
        var datasetId = CreateTempDataset();
        var tableId = CreateTempEmptyTable(datasetId);
        string filePath = Path.Combine("data", "sample.csv");
        snippet.LoadFromFile(_projectId, datasetId, tableId, filePath);
        var output = stringOut.ToString();
        long numRows = Convert.ToInt64(Regex.Match(output, @"\d+").Value);
        Assert.Equal(4, numRows);
    }

    [Fact]
    public void TestLoadTableGcsCsv()
    {
        var snippet = new BigQueryLoadTableGcsCsv();
        var datasetId = CreateTempDataset();
        snippet.LoadTableGcsCsv(_projectId, datasetId);
        var output = stringOut.ToString();
        long numRows = Convert.ToInt64(Regex.Match(output, @"\d+").Value);
        Assert.Equal(50, numRows);
    }

    [Fact]
    public void TestLoadTableGcsJson()
    {
        var snippet = new BigQueryLoadTableGcsJson();
        var datasetId = CreateTempDataset();
        snippet.LoadTableGcsJson(_projectId, datasetId);
        var output = stringOut.ToString();
        long numRows = Convert.ToInt64(Regex.Match(output, @"\d+").Value);
        Assert.Equal(50, numRows);
    }

    [Fact]
    public void TestLoadTableGcsOrc()
    {
        var snippet = new BigQueryLoadTableGcsOrc();
        var datasetId = CreateTempDataset();
        snippet.LoadTableGcsOrc(_projectId, datasetId);

        var truncateSnippet = new BigQueryLoadTableGcsOrcTruncate();
        truncateSnippet.LoadTableGcsOrcTruncate(
            _projectId, datasetId, "us_states");

        var output = stringOut.ToString();
        // Snippet runs should report 50 output rows twice
        Assert.Equal(2, Regex.Matches(output, "50").Count);
    }

    [Fact]
    public void TestQuery()
    {
        var snippet = new BigQueryQuery();
        snippet.Query(_projectId);
        var outputLines = stringOut.ToString().Trim().Split(new [] { '\n' });
        Assert.Equal(100, outputLines.Count());
    }

    [Fact]
    public void TestQueryLegacy()
    {
        var snippet = new BigQueryQueryLegacy();
        snippet.QueryLegacy(_projectId);
        var outputLines = stringOut.ToString().Trim().Split(new [] { '\n' });
        Assert.Equal(100, outputLines.Count());
    }

    [Fact]
    public void TestTableInsertRows()
    {
        var snippet = new BigQueryTableInsertRows();
        var datasetId = CreateTempDataset();
        var tableId = CreateTempUsStatesTable(datasetId);
        snippet.TableInsertRows(_projectId, datasetId, tableId);
    }

    public string CreateTempDataset()
    {
        string datasetId = TestUtil.RandomName();
        BigQueryDataset tempDataset = _client.CreateDataset(
            datasetId, new CreateDatasetOptions() { Location = "US" });
        tempDatasets.Add(tempDataset);
        return datasetId;
    }

    public string CreateTempEmptyTable(string datasetId)
    {
        string tableId = TestUtil.RandomName();
        BigQueryTable table = _client.CreateTable(datasetId, tableId, null);
        return tableId;
    }

    public string CreateTempUsStatesTable(string datasetId)
    {
        var snippet = new BigQueryLoadTableGcsCsv();
        snippet.LoadTableGcsCsv(_projectId, datasetId);
        return "us_states";  // The table ID defined in the snippet
    }
    public void Dispose()
    {
        foreach (BigQueryDataset dataset in tempDatasets) {
            var deleteDatasetOptions = new DeleteDatasetOptions()
            {
                DeleteContents = true
            };
            dataset.Delete(deleteDatasetOptions);
        }
        _storage.DeleteBucket(
            _bucketName,
            new DeleteBucketOptions() { DeleteObjects = true }
        );
        Console.SetOut(consoleOut);
    }
}
