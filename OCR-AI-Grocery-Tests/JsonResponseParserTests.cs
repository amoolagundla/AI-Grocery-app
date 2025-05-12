using Microsoft.Extensions.Logging;
using Moq;
using OCR_AI_Grocey.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Google.Apis.Requests.BatchRequest;

namespace OCR_AI_Grocery_Tests
{
    public class JsonResponseParserTests
    {
        private readonly Mock<ILogger<JsonResponseParser>> _mockLogger;
        private readonly JsonResponseParser _parser;

        public JsonResponseParserTests()
        {
            _mockLogger = new Mock<ILogger<JsonResponseParser>>();
            _parser = new JsonResponseParser(_mockLogger.Object);
        }

        [Fact]
        public void ParseOpenAIResponse_ShouldReturnExpectedStoreData()
        {
            // Arrange
            string jsonResponse = @"{
  ""stores"": {
    ""Patel Brothers"": {
      ""items"": [
        ""Poha Thick"",
        ""Maggi Masala"",
        ""Rice Flour"",
        ""Organic Tur Dal"",
        ""Idli Rava"",
        ""Soji Coarse"",
        ""Urad Whole White"",
        ""Deep Shred Coconut"",
        ""Brin Mik Bites"",
        ""Dosakai"",
        ""Indian Eggplant"",
        ""Organic Ginger"",
        ""Fresh Spinach"",
        ""Indian Okra"",
        ""Green Onion"",
        ""Tindora"",
        ""Cilantro"",
        ""Fresh Mint Leaves"",
        ""Fresh Methi"",
        ""Small Chilli"",
        ""Green Bell Pepper"",
        ""Opo Dudhi"",
        ""Red Onion"",
        ""Green Beans""
      ],
      ""prices"": [
        5.99,
        3.98,
        3.99,
        7.99,
        4.99,
        7.99,
        12.99,
        4.99,
        1.98,
        0.61,
        0.77,
        2.33,
        3.98,
        2.31,
        0.66,
        4.83,
        0.32,
        1.98,
        1.29,
        1.43,
        0.86,
        3.30,
        2.49,
        1.33
      ],
      ""purchase_date"": ""2025-03-30"",
      ""transaction_id"": null
    },
    ""Kroger"": {
      ""items"": [
        ""Sara Butter Bread"",
        ""VTL Farms Eggs"",
        ""Baby Spinach"",
        ""Goldminer Bread""
      ],
      ""prices"": [
        4.49,
        7.99,
        5.99,
        4.99
      ],
      ""purchase_date"": ""2025-03-26"",
      ""transaction_id"": null
    },
    ""Walmart"": {
      ""items"": [
        ""Cotton"",
        ""Swaddle Wrap"",
        ""Baby Burp"",
        ""Baby Layette"",
        ""Baby Wipes"",
        ""Oat Raisin Cookie"",
        ""Organic Salad"",
        ""Ubbi Pail""
      ],
      ""prices"": [
        15.92,
        1.98,
        7.48,
        7.48,
        2.34,
        3.47,
        2.98,
        69.00
      ],
      ""purchase_date"": ""2025-03-22"",
      ""transaction_id"": null
    },
    ""Braum's"": {
      ""items"": [
        ""Milk"",
        ""2% Milk"",
        ""Natural Whole Almonds"",
        ""Red Onions"",
        ""Mexican Shredded Cheese"",
        ""Ruffles Potato Chips"",
        ""Silk Almond Unsweetened""
      ],
      ""prices"": [
        4.69,
        4.69,
        5.99,
        2.15,
        2.19,
        5.19,
        4.29
      ],
      ""purchase_date"": ""2025-03-09"",
      ""transaction_id"": null
    },
    ""Hareli Fresh Market"": {
      ""items"": [
        ""Halal Baby Goat Mix"",
        ""Kinder J Chili Thai"",
        ""Halal Chicken Breast""
      ],
      ""prices"": [
        20.65,
        2.49,
        8.22
      ],
      ""purchase_date"": ""2025-03-14"",
      ""transaction_id"": null
    },
    ""Hello! India"": {
      ""items"": [
        ""Irani Chai Large"",
        ""Mysore Bajji - 3 Pieces""
      ],
      ""prices"": [
        4.00,
        5.99
      ],
      ""purchase_date"": ""2025-03-11"",
      ""transaction_id"": null
    }
  }
}
             ";

            // Act
            var result = _parser.ParseOpenAIResponse(jsonResponse);

            // Assert
            Assert.NotNull(result);
            
        }

        [Fact]
        public void ParseOpenAIResponseForTimeSeries_ShouldConvertToTimeSeriesFormat()
        {
            // Arrange
            string jsonResponse = @"{
  ""stores"": {
    ""Patel Brothers"": {
      ""items"": [
        ""Poha Thick"",
        ""Maggi Masala"",
        ""Rice Flour"",
        ""Organic Tur Dal"",
        ""Idli Rava"",
        ""Soji Coarse"",
        ""Urad Whole White"",
        ""Deep Shred Coconut"",
        ""Brin Mik Bites"",
        ""Dosakai"",
        ""Indian Eggplant"",
        ""Organic Ginger"",
        ""Fresh Spinach"",
        ""Indian Okra"",
        ""Green Onion"",
        ""Tindora"",
        ""Cilantro"",
        ""Fresh Mint Leaves"",
        ""Fresh Methi"",
        ""Small Chilli"",
        ""Green Bell Pepper"",
        ""Opo Dudhi"",
        ""Red Onion"",
        ""Green Beans""
      ],
      ""prices"": [
        5.99,
        3.98,
        3.99,
        7.99,
        4.99,
        7.99,
        12.99,
        4.99,
        1.98,
        0.61,
        0.77,
        2.33,
        3.98,
        2.31,
        0.66,
        4.83,
        0.32,
        1.98,
        1.29,
        1.43,
        0.86,
        3.30,
        2.49,
        1.33
      ],
      ""purchase_date"": ""2025-03-30"",
      ""transaction_id"": null
    },
    ""Kroger"": {
      ""items"": [
        ""Sara Butter Bread"",
        ""VTL Farms Eggs"",
        ""Baby Spinach"",
        ""Goldminer Bread""
      ],
      ""prices"": [
        4.49,
        7.99,
        5.99,
        4.99
      ],
      ""purchase_date"": ""2025-03-26"",
      ""transaction_id"": null
    },
    ""Walmart"": {
      ""items"": [
        ""Cotton"",
        ""Swaddle Wrap"",
        ""Baby Burp"",
        ""Baby Layette"",
        ""Baby Wipes"",
        ""Oat Raisin Cookie"",
        ""Organic Salad"",
        ""Ubbi Pail""
      ],
      ""prices"": [
        15.92,
        1.98,
        7.48,
        7.48,
        2.34,
        3.47,
        2.98,
        69.00
      ],
      ""purchase_date"": ""2025-03-22"",
      ""transaction_id"": null
    },
    ""Braum's"": {
      ""items"": [
        ""Milk"",
        ""2% Milk"",
        ""Natural Whole Almonds"",
        ""Red Onions"",
        ""Mexican Shredded Cheese"",
        ""Ruffles Potato Chips"",
        ""Silk Almond Unsweetened""
      ],
      ""prices"": [
        4.69,
        4.69,
        5.99,
        2.15,
        2.19,
        5.19,
        4.29
      ],
      ""purchase_date"": ""2025-03-09"",
      ""transaction_id"": null
    },
    ""Hareli Fresh Market"": {
      ""items"": [
        ""Halal Baby Goat Mix"",
        ""Kinder J Chili Thai"",
        ""Halal Chicken Breast""
      ],
      ""prices"": [
        20.65,
        2.49,
        8.22
      ],
      ""purchase_date"": ""2025-03-14"",
      ""transaction_id"": null
    },
    ""Hello! India"": {
      ""items"": [
        ""Irani Chai Large"",
        ""Mysore Bajji - 3 Pieces""
      ],
      ""prices"": [
        4.00,
        5.99
      ],
      ""purchase_date"": ""2025-03-11"",
      ""transaction_id"": null
    }
  }
}
             ";

            // Act
            var result = _parser.ParseOpenAIResponseForTimeSeries(jsonResponse);

            // Assert
            Assert.NotNull(result);
             
        }
    }
}
