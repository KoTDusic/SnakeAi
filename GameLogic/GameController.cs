using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameLogic.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GameLogic
{
    public class GameController
    {
        private const int GameFieldWidth = 20;
        private const int GameFieldHeight = 20;
        private const int MsTimeout = 100;
        private readonly BlockType[,] _gameField = new BlockType[GameFieldHeight, GameFieldWidth];
        private readonly List<KeyValuePair<int,int>> _snake = new List<KeyValuePair<int, int>>();
        private readonly Action _exit;
        private readonly TextBlock _gameInviteTextBlock = new TextBlock();
        private readonly TextBlock _gameOverTextBlock = new TextBlock();
        private readonly TextBlock _scoreTextBlock = new TextBlock();
        private int _foodEaten;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private GameState _gameState = GameState.MainMenu;
        private Direction PressedDirection { get; set; }
        private int FoodEaten
        {
            get => _foodEaten;
            set
            {
                _foodEaten = value;
                UpdateScore();
            }
        }

        public GameController(Action exit)
        {
            _exit = exit;
        }
        
        private async Task GameCycle(CancellationToken ctsToken)
        {
            while (!ctsToken.IsCancellationRequested)
            {
                await Task.Delay(MsTimeout, ctsToken).ConfigureAwait(false);
                IterateField(ctsToken);
            }
        }

        private void IterateField(CancellationToken ctsToken)
        {
            if (ctsToken.IsCancellationRequested)
            {
                return;
            }

            var eatFood = false;
            var oldHeadPos = _snake.Last();
            var newHeadPos = GetNewHeadPosition(oldHeadPos, GetNextSnakeDirection());
            var collisionBlock = _gameField[newHeadPos.Key, newHeadPos.Value];
            switch (collisionBlock)
            {
                case BlockType.Block:
                case BlockType.SnakeBody:
                case BlockType.SnakeHead:
                {
                    StopGame();
                    break;
                }
                case BlockType.Food:
                {
                    eatFood = true;
                    FoodEaten++;
                    break;
                }
            }

            _snake.Add(newHeadPos);
            _gameField[newHeadPos.Key, newHeadPos.Value] = BlockType.SnakeHead;
            _gameField[oldHeadPos.Key, oldHeadPos.Value] = BlockType.SnakeBody;
            if (eatFood)
            {
                GenerateFood();
            }
            else
            {
                _gameField[_snake[0].Key, _snake[0].Value] = BlockType.Air;
                _snake.RemoveAt(0);
            }
        }

        private void UpdateScore()
        {
            _scoreTextBlock.Text = _foodEaten.ToString();
        }

        private void StartGame()
        {
            FoodEaten = 0;
            ResetField();
            _gameState = GameState.Game;
            GameCycle(_cts.Token).Forget();
        }
        
        private void StopGame()
        {
            _gameOverTextBlock.Text =  $"Game Over! You eat {FoodEaten} food." +
                                       " \nPress \"Enter\" to start";
            _scoreTextBlock.Text = string.Empty;
            _gameState = GameState.GameOver;
            PressedDirection = Direction.Unassigned;
            FoodEaten = 0;
            UpdateScore();
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }
        
        private void ResetField()
        {
            for (var i = 0; i < GameFieldHeight; i++)
            {
                for (var j = 0; j <GameFieldWidth; j++)
                {
                    if (i == 0 || j == 0 || i == GameFieldWidth - 1 || j == GameFieldHeight - 1)
                    {
                        _gameField[i, j] = BlockType.Block;
                    }
                    else
                    {
                        _gameField[i, j] = BlockType.Air;
                    }
                }
            }
            
            _snake.Clear();
            _snake.Add(new KeyValuePair<int, int>(GameFieldHeight/2+1, GameFieldWidth/2));
            _snake.Add(new KeyValuePair<int, int>(GameFieldHeight/2, GameFieldWidth/2));
            _snake.Add(new KeyValuePair<int, int>(GameFieldHeight/2-1, GameFieldWidth/2));
            
            _gameField[_snake[2].Key, _snake[2].Value] = BlockType.SnakeHead;
            _gameField[_snake[1].Key, _snake[1].Value] = BlockType.SnakeBody;
            _gameField[_snake[0].Key, _snake[0].Value] = BlockType.SnakeBody;
            GenerateFood();
        }

        public void Initialize(ContentManager content)
        {
            _gameInviteTextBlock.Initialize(content);
            _gameOverTextBlock.Initialize(content);
            _scoreTextBlock.Initialize(content);
            _gameInviteTextBlock.Text = "Press \"Enter\" to start";
        }

        public void Update(GameTime gameTime, MouseState mouse, KeyboardState keyboard)
        {
            if (keyboard.IsKeyDown(Keys.Escape))
            {
                _exit();
            }

            switch (_gameState)
            {
                case GameState.MainMenu:
                {
                    if (keyboard.IsKeyDown(Keys.Enter))
                    {
                        StartGame();
                    }
                    
                    break;
                }
                case GameState.Game:
                {
                    var currentPressedDirection = GetPressedDirection(keyboard);
                    if (currentPressedDirection != Direction.Unassigned)
                    {
                        PressedDirection = currentPressedDirection;
                    }
                    
                    break;
                }
                case GameState.GameOver:
                {
                    if (keyboard.IsKeyDown(Keys.Enter))
                    {
                        StartGame();
                    }

                    break;
                }
            }
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch, Rectangle viewportBounds)
        {
            switch (_gameState)
            {
                case GameState.MainMenu:
                {
                    _gameInviteTextBlock.Draw(spriteBatch, viewportBounds);
                    break;
                }
                case GameState.Game:
                {
                    var gamePart = viewportBounds.GetTopLeftPart(
                        viewportBounds.Width / 3 * 2,
                        viewportBounds.Height);
                    var scorePart = viewportBounds.GetTopRightPart(viewportBounds.Width / 3,
                        viewportBounds.Height);
                    var tileHeight = gamePart.Height / GameFieldHeight;
                    var tileWidth = gamePart.Width / GameFieldWidth;
                    var tileSize = Math.Min(tileWidth, tileHeight);
                    gamePart = new Rectangle(0, 0,
                        tileSize * GameFieldWidth,
                        tileSize * GameFieldHeight);
                    spriteBatch.Draw(EmptyTexture.Texture, gamePart, Color.LightGray);
                    for (var i = 0; i < GameFieldHeight; i++)
                    {
                        for (var j = 0; j < GameFieldWidth; j++)
                        {
                            var blockRectangle = new Rectangle(
                                gamePart.X + j * tileSize,
                                gamePart.Y + i * tileSize,
                                tileSize - 1, tileSize - 1);
                            switch (_gameField[i, j])
                            {
                                case BlockType.SnakeBody:
                                {
                                    spriteBatch.Draw(EmptyTexture.Texture, blockRectangle, Color.BlueViolet);
                                    break;
                                }
                                case BlockType.SnakeHead:
                                {
                                    spriteBatch.Draw(EmptyTexture.Texture, blockRectangle, Color.Blue);
                                    break;
                                }
                                case BlockType.Air:
                                {
                                    spriteBatch.Draw(EmptyTexture.Texture, blockRectangle, Color.White);
                                    break;
                                }
                                case BlockType.Block:
                                {
                                    spriteBatch.Draw(EmptyTexture.Texture, blockRectangle, Color.Black);
                                    break;
                                }
                                case BlockType.Food:
                                {
                                    spriteBatch.Draw(EmptyTexture.Texture, blockRectangle, Color.Red);
                                    break;
                                }
                            }
                        }
                    }
                    
                    _scoreTextBlock.Draw(spriteBatch, scorePart);
                    
                    break;
                }
                case GameState.GameOver:
                {
                    _gameOverTextBlock.Draw(spriteBatch, viewportBounds);
                    break;
                }
            }
        }
        
        private Direction GetNextSnakeDirection()
        {
            KeyValuePair<int, int> headCords, bodyCords;
            headCords = _snake.Last();
            bodyCords = _snake[_snake.Count - 2];
            var snakeDirection = GetSnakeDirection(headCords, bodyCords);
            switch (snakeDirection)
            {
                case Direction.Left:
                case Direction.Right:
                {
                    if (PressedDirection != Direction.Left && 
                        PressedDirection != Direction.Right && 
                        PressedDirection != Direction.Unassigned)
                    {
                        return PressedDirection;
                    }

                    return snakeDirection;
                }
                case Direction.Up:
                case Direction.Down:
                {
                    if (PressedDirection != Direction.Up && 
                        PressedDirection != Direction.Down &&
                        PressedDirection != Direction.Unassigned)
                    {
                        return PressedDirection;
                    }

                    return snakeDirection;
                }
                default:
                {
                    return snakeDirection;
                }
            }
        }
        
        private void GenerateFood()
        {
            var availableFields = new List<KeyValuePair<int, int>>();
            for (var i = 0; i < GameFieldHeight; i++)
            {
                for (var j = 0; j < GameFieldWidth; j++)
                {
                    if (_gameField[i, j] == BlockType.Air)
                    {
                        availableFields.Add(new KeyValuePair<int, int>(i, j));
                    }
                }
            }

            var targetPoint = availableFields[RandomHelper.Next(0, availableFields.Count - 1)];
            _gameField[targetPoint.Key, targetPoint.Value] = BlockType.Food;
        }
        
        private static Direction GetPressedDirection(KeyboardState keyboard)
        {
            if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
            {
                return Direction.Left;
            }
            if (keyboard.IsKeyDown(Keys.Right)|| keyboard.IsKeyDown(Keys.D))
            {
                return Direction.Right;
            }
            if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S))
            {
                return Direction.Down;
            }
            if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W))
            {
                return Direction.Up;
            }

            return Direction.Unassigned;
        }

        private static Direction GetSnakeDirection(KeyValuePair<int, int> head, KeyValuePair<int, int> body)
        {
            if (body.Key - head.Key == 1)
            {
                return Direction.Up;
            }

            if (body.Key - head.Key == -1)
            {
                return Direction.Down;
            }
            
            if (body.Value - head.Value == 1)
            {
                return Direction.Left;
            }
            
            if (body.Value - head.Value == -1)
            {
                return Direction.Right;
            }

            return Direction.Unassigned;
        }

        private static KeyValuePair<int, int> GetNewHeadPosition(KeyValuePair<int, int> head, Direction direction)
        {
            switch (direction)
            {
                case Direction.Left:
                {
                    return new KeyValuePair<int, int>(head.Key, head.Value - 1);
                }
                case Direction.Up:
                {
                    return new KeyValuePair<int, int>(head.Key - 1, head.Value);
                }
                case Direction.Right:
                {
                    return new KeyValuePair<int, int>(head.Key, head.Value + 1);
                }
                case Direction.Down:
                {
                    return new KeyValuePair<int, int>(head.Key + 1, head.Value);
                }
                default:
                {
                    throw new ArgumentException(
                        $"Не удалось определить направление движения direction = {direction}");
                }
            }
        }
    }
}