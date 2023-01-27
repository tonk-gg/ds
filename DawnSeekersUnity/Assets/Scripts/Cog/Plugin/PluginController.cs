using UnityEngine;
using Cog.GraphQL;
using GraphQL4Unity;
using Newtonsoft.Json.Linq;
using Cog.Account;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Contracts;
using System;
using System.Text;
using System.Linq;

namespace Cog
{
    public class PluginController : MonoBehaviour
    {
        private const string DEFAULT_GAME_ID = "latest";

        public static PluginController Instance;

        [SerializeField]
        private GraphQLHttp _client;

        [SerializeField]
        private GraphQLWebsocket _clientWS;

        [SerializeField]
        private string _gameID = "";

        [SerializeField]
        private string _privateKey = "0xc14c1284a5ff47ce38e2ad7a50ff89d55ca360b02cdf3756cdb457389b1da223";

        public string Account { get; private set; }

        public State State { get; private set; }

        // -- Events
        public Action<State> StateUpdated;

        protected void Awake()
        {
            Instance = this;
        }

        protected void Start()
        {
            if (_client != null)
            {
                _client.URL = "http://localhost:8080/query";
            }

            if (_clientWS != null)
            {
                _clientWS.Url = "ws://localhost:8080/query";
                _clientWS.OpenEvent += OnSocketOpened;
                _clientWS.CloseEvent += OnSocketClosed;
                _clientWS.Connect = true;
            }
        }
        public void DispatchAction(string actionName, params object[] args)
        {
            // Debug.Log($"PluginController::DispatchAction() actionName: {actionName} args:");
            // foreach(var arg in args) {
            //     Debug.Log(arg);
            // }

#if  UNITY_EDITOR
            // -- Directly dispatch the action via GraphQL
            DirectDispatchAction(actionName, args);
#else
            // -- Send message up to shell and let it do the signing and sending
            Debug.Log("PluginController::DispatchAction() Sending Dispatch message...")
#endif
        }

        public void FetchState()
        {
            var gameID = "latest";
            if (_gameID != "") gameID = _gameID;
            
            var variables = new JObject
            {
                {"gameID", gameID}
            };

            _client.ExecuteQuery(Operations.FetchStateDocument, variables, (response) =>
            {
                // Debug.Log($"Graph response: {response.Result.ToString()}");

                switch (response.Type)
                {
                    case MessageType.GQL_DATA:
                        // Debug.Log("GQL_DATA");

                        // Deserialise here
                        var result = response.Result.Data.ToObject<FetchStateQuery>();
                        UpdateState(result.Game.State);
                        break;

                    case MessageType.GQL_ERROR:
                        Debug.Log("GQL_ERROR");
                        break;

                    case MessageType.GQL_COMPLETE:
                        Debug.Log("GQL_COMPLETE");

                        break;
                    case MessageType.GQL_EXCEPTION:
                        Debug.Log("GQL_EXCEPTION");

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }

        public void UpdateState(State state) 
        {
            State = state;
            if (StateUpdated != null)
            {
                StateUpdated.Invoke(state);
            }
        }

        private void StartStateListener() 
        {
            var gameID = (_gameID != "")? _gameID : DEFAULT_GAME_ID;
            
            var variables = new JObject
            {
                {"gameID", gameID}
            };

            _clientWS.ExecuteQuery(Operations.OnStateSubscription, variables, (response) =>
            {
                // Debug.Log($"Graph response: {response.Result.ToString()}");

                switch (response.Type)
                {
                    case MessageType.GQL_DATA:
                        // Debug.Log("GQL_DATA");

                        // Deserialise here
                        Debug.Log(response.Result.Data);
                        var result = response.Result.Data.ToObject<OnStateSubscription>();
                        UpdateState(result.State);

                        break;

                    case MessageType.GQL_ERROR:
                        foreach (var error in response.Result.Errors)
                        {
                            DisplayError(error.ToString());
                        }
                        break;
                    case MessageType.GQL_COMPLETE:
                        DisplayMessage($"Complete {response}");
                        break;
                    case MessageType.GQL_EXCEPTION:
                        DisplayError($"Exception {response.Result}");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });

            FetchState();
        }

        // -- AUTH FOR STANDALONE

        /**
         *  Used when running Unity in the editor so graphQL mutations can be made
         */
        private void AuthorizePublicKey()
        {
            Debug.Log("PluginController::AuthorizePublicKey()");

            // build a session auth message

            var sessionAddress = AccountManager.Instance.SessionPublicKey.HexToByteArray();
            Debug.Log("PluginController::AuthorizePublicKey(): " + sessionAddress.ToHex(true));

            var signInMessage = Encoding.Unicode.GetBytes("You are signing in with session: ");
            var authMessage = Sha3Keccack.Current.CalculateHash(
                Encoding.Unicode.GetBytes($"\x19Ethereum Signed Message:\n{signInMessage.Length + 20}")
                    .Concat(signInMessage)
                    .Concat(sessionAddress)
                    .ToArray()
            );

            // sign it and submit mutation
            AccountManager.Instance.SignMessage(authMessage.ToString(), (signedMessage) =>
            {
                var gameID = (_gameID != "")? _gameID : DEFAULT_GAME_ID;
                var variables = new JObject
                {
                    {"gameID", gameID},
                    {"session", AccountManager.Instance.SessionPublicKey},
                    {"auth", signedMessage},
                };
                _client.ExecuteQuery(Operations.SigninDocument, variables, (response) =>
                {
                    switch (response.Type)
                    {
                        case MessageType.GQL_DATA:
                            var success = bool.Parse(response.Result.Data["signin"]?.ToString() ?? string.Empty);

                            if (success)
                            {
                                Debug.Log("PluginController: Successfully signed into COG");
                                OnPublicKeyAuthorized();
                            }
                            
                            break;
                        case MessageType.GQL_ERROR:
                            foreach (var error in response.Result.Errors)
                            {
                                DisplayError(error.ToString());
                            }
                            break;
                        case MessageType.GQL_COMPLETE:
                            DisplayMessage($"Complete {response}");
                            break;
                        case MessageType.GQL_EXCEPTION:
                            DisplayError($"Exception {response.Result}");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                });
            }, DisplayError);
        }
        
        private void DisplayMessage(object message)
        {
            Debug.Log(message);
        }
        
        private void DisplayError(object error)
        {
            Debug.LogError(error);
        }

/*
        const action = actions.encodeFunctionData(actionName, actionArgs);
        const actionDigest = ethers.utils.arrayify(ethers.utils.keccak256(action));
        const auth = await session.signMessage(actionDigest);
        return client.mutate({ mutation: DispatchDocument, variables: { gameID, auth, action } }).then((res) => {
            console.log('dispatched', actionName);
            return res.data.dispatch;
        });
*/

        private void DirectDispatchAction(string actionName, params object[] args)
        {
            // TODO: Use code reflection to get the action as they have decorators so should be possible?
            switch (actionName)
            {
                case "MOVE_SEEKER":
                    var action = new Cog.Actions.MoveSeekerAction();
                    action.sid = (string)args[0];
                    action.q = (int)args[1];
                    action.r = (int)args[2];
                    action.s = (int)args[3];

                    var actionBytes = action.GetCallData();
                    // var actionDigest = Sha3Keccack.Current.CalculateHash(actionBytes);

                    Debug.Log("PluginController:DirectDispatchAction: " + actionBytes.ToHex(true));

                    AccountManager.Instance.HashAndSignSession(actionBytes, (auth) =>
                    {
                        var gameID = (_gameID != "")? _gameID : DEFAULT_GAME_ID;
                        var variables = new JObject
                        {
                            {"gameID", gameID},
                            {"action", actionBytes},
                            {"auth", auth}
                        };
                        _client.ExecuteQuery(Operations.DispatchDocument, variables, (response) =>
                        {
                            switch (response.Type)
                            {
                                case MessageType.GQL_DATA:
                                    DisplayMessage($"Data {response}");
                                    Debug.Log(response.Result);
                                    break;
                                case MessageType.GQL_ERROR:
                                    foreach (var error in response.Result.Errors)
                                    {
                                        DisplayError(error.ToString());
                                    }
                                    break;
                                case MessageType.GQL_COMPLETE:
                                    DisplayMessage($"Complete {response}");
                                    break;
                                case MessageType.GQL_EXCEPTION:
                                    DisplayError($"Exception {response.Result}");
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        });
                    }, 
                    DisplayError
                    );
                break;
            }
        }

        // -- LISTENERS

        public void OnTileClick(Vector3Int tileCubeCoords)
        {
            // Debug.Log("PluginController::OnTileClick() tileCubeCoords: " + tileCubeCoords);

            //-- Send out message here
            // Debug.Log("PluginController::OnTileClick() Sending message TILE_INTERACTION");
        }

        private void OnSocketOpened()
        {
            Debug.Log("PluginController: Socket opened");

#if  UNITY_EDITOR
            // -- Authorise the public key to allow for state mutations from within the Unity editor
            InitWalletProvider();
#else
            // -- State mutations handled by shell so skip auth and subscribe to state updates
            StartStateListener();
#endif
        }

        private void InitWalletProvider()
        {
            Debug.Log("PluginController::InitWalletProvider()");
            // Debug.Log("Priv key: " + _privateKey);
            
            AccountManager.Instance.InitProvider(
                WalletProviderEnum.PRIVATE_KEY,
                _privateKey
            );

            AccountManager.Instance.ConnectedEvent += OnWalletConnect;
            AccountManager.Instance.Connect();

            Account = AccountManager.Instance.Account;
        }

        private void OnWalletConnect()
        {
            AuthorizePublicKey();
        }

        private void OnPublicKeyAuthorized()
        {
            Debug.Log("PluginController::OnPublicKeyAuthorized()");
            StartStateListener();
        }

        private void OnSocketClosed()
        {
            Debug.Log("PluginController: Socket closed");
        }
    }

}