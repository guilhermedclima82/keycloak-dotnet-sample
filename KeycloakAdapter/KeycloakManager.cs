﻿using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KeycloakAdapter
{
    public class KeycloakManager
    {
        private readonly string _baseAddress;
        private readonly string _urlAddRoleToUser;
        private readonly string _initialAccessAddress;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _createUserUrl;
        private readonly Role[] _roles;

        public KeycloakManager(IConfiguration configutation)
        {
            _baseAddress = configutation["keycloakData:UrlBase"];
            _urlAddRoleToUser = _baseAddress + configutation["keycloakData:UrlAddRoleToUser"];

            _initialAccessAddress = _baseAddress + configutation["keycloakData:SessionStartUrl"];
            _clientId = configutation["keycloakData:ClientId"];
            _clientSecret = configutation["keycloakData:ClientSecret"];
            _createUserUrl = _baseAddress + configutation["keycloakData:CreateUserUrl"];

            _roles = System.Text.Json.JsonSerializer.Deserialize<Role[]>(configutation["keycloakData:Roles"]);
        }

        public string InitialAccessAddress { get => _initialAccessAddress; }
        public string ClientId { get => _clientId; }
        public string ClientSecret { get => _clientSecret; }
        public string CreateUserUrl { get => _createUserUrl; }

        public IEnumerable<KeyValuePair<string, string>> GetHeaderSessionStart(string login, string password)
        {
            IEnumerable<KeyValuePair<string, string>> headerData = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("username", login),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
            };

            return headerData;
        }

        public async Task<OpenIdConnect> TryLoginExecute(string login, string password)
        {
            using var httpClient = new HttpClient();
            using var content = new FormUrlEncodedContent(GetHeaderSessionStart(login, password));

            content.Headers.Clear();
            content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

            HttpResponseMessage response = await httpClient.PostAsync(_initialAccessAddress, content);
            string answer = await response.Content.ReadAsStringAsync();

            return GetAccessResult(answer);
        }

        public async Task<int> TryCreateUser(string jwt, StringContent httpConent)
        {
            int statusCode = default;

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", jwt);
                HttpResponseMessage response = await httpClient.PostAsync(_createUserUrl, httpConent);
                statusCode = (int)response.StatusCode;

                string answer = await response.Content.ReadAsStringAsync();

                OpenIdConnect openIdConnect = JsonConvert.DeserializeObject<OpenIdConnect>(answer);

                if (openIdConnect != null && openIdConnect.HasError) answer = openIdConnect.error_description ?? openIdConnect.errorMessage;
            }
            catch (Exception)
            { }

            return statusCode;
        }
        public async Task<int> TryAddRole(string jwt, User user, string roleName)
        {
            Role roleToAdd = this._roles.FirstOrDefault(r => r.name.Equals(roleName));

            int statusCode = default;
            var _roles = new Role[] { roleToAdd };
            string listOfRole = JsonConvert.SerializeObject(_roles);

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", jwt);

                string url = _urlAddRoleToUser
                    .Replace("[USER_UUID]", user.Id)
                    .Replace("[CLIENT_UUID]", roleToAdd.containerId);

                StringContent httpConent = new StringContent(listOfRole, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await httpClient.PostAsync(url, httpConent);
                statusCode = (int)response.StatusCode;

                string answer = await response.Content.ReadAsStringAsync();

                OpenIdConnect openIdConnect = JsonConvert.DeserializeObject<OpenIdConnect>(answer);

                if (openIdConnect != null && openIdConnect.HasError) answer = openIdConnect.error_description ?? openIdConnect.errorMessage;
            }
            catch (Exception)
            { }

            return statusCode;
        }

        public static async Task<HttpResponseObject<User>> FindUserByEmail(string jwt, string email)
        {
            HttpResponseObject<User> responseSearch = new HttpResponseObject<User>();

            List<User> userResponse;

            try
            {
                using var httpClient = new HttpClient();
                string url = $"http://localhost:8080/auth/admin/realms/master/users?email={email}";

                httpClient.DefaultRequestHeaders.Add("Authorization", jwt);
                HttpResponseMessage response = await httpClient.GetAsync(url);

                int statusCode = (int)response.StatusCode;

                string answer = await response.Content.ReadAsStringAsync();

                userResponse = JsonConvert.DeserializeObject<List<User>>(answer);

                User finalResponse = (userResponse.Any()) ? userResponse.FirstOrDefault(u => !string.IsNullOrEmpty(u.email)) : null;

                responseSearch.Create(statusCode, finalResponse);
            }
            catch (Exception)
            { }

            return responseSearch;
        }

        public static OpenIdConnect GetAccessResult(string answer)
        {
            return JsonConvert.DeserializeObject<OpenIdConnect>(answer);
        }

        public static string CreateUserData(User user)
        {
            StringBuilder userJson = new StringBuilder();
            userJson.Append("{ \"firstName\":\"");
            userJson.Append(user.firstName);
            userJson.Append("\",\"lastName\":\"");
            userJson.Append(user.lastName);
            userJson.Append("\",\"email\":\"");
            userJson.Append(user.email);
            userJson.Append("\",\"enabled\":\"true\", \"emailVerified\":\"true\",\"username\":\"");
            userJson.Append(user.username);
            userJson.Append("\",\"credentials\": [{ \"type\": \"password\",\"value\":\"");
            userJson.Append(user.password);
            userJson.Append("\",\"temporary\": false}]}");

            return userJson.ToString();
        }

    }
}
