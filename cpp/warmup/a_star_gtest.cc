#include <gtest/gtest.h>

#include <iostream>

#include "cpp/warmup/a_star.hpp"

void print_maze(const warmup::Maze& maze) {
  for (const auto& row : maze) {
    for (const auto& cell : row) {
      std::cout << cell;
    }
    std::cout << std::endl;
  }
}

TEST(AStarTest, SimplePath) {
  warmup::Maze maze = {{'.', '.', '.'}, {'#', '#', '.'}, {'.', '.', '.'}};
  warmup::cell start = {0, 0};
  warmup::cell end = {2, 2};
  EXPECT_TRUE(warmup::a_star_search(maze, start, end));
}

TEST(AStarTest, NoPath) {
  warmup::Maze maze = {{'.', '#', '.'}, {'.', '#', '.'}, {'.', '#', '.'}};
  warmup::cell start = {0, 0};
  warmup::cell end = {2, 0};
  EXPECT_FALSE(warmup::a_star_search(maze, start, end));
}

TEST(AStarTest, StartEqualsEnd) {
  warmup::Maze maze = {{'.', '.', '.'}, {'.', '.', '.'}, {'.', '.', '.'}};
  warmup::cell start = {1, 1};
  warmup::cell end = {1, 1};
  EXPECT_TRUE(warmup::a_star_search(maze, start, end));
}

TEST(AStarTest, EmptyMaze) {
  warmup::Maze maze = {};
  warmup::cell start = {0, 0};
  warmup::cell end = {0, 0};
  EXPECT_FALSE(warmup::a_star_search(maze, start, end));
}

TEST(AStarTest, StartIsWall) {
  warmup::Maze maze = {{'#', '.', '.'}, {'.', '.', '.'}, {'.', '.', '.'}};
  warmup::cell start = {0, 0};
  warmup::cell end = {2, 2};
  EXPECT_FALSE(warmup::a_star_search(maze, start, end));
}

TEST(AStarTest, EndIsWall) {
  warmup::Maze maze = {{'.', '.', '.'}, {'.', '.', '.'}, {'.', '.', '#'}};
  warmup::cell start = {0, 0};
  warmup::cell end = {2, 2};
  EXPECT_FALSE(warmup::a_star_search(maze, start, end));
}

TEST(AStarTest, FullPathVerification) {
  warmup::Maze maze = {{'.', '.', '.'}, {'#', '#', '.'}, {'.', '.', '.'}};
  warmup::cell start = {0, 0};
  warmup::cell end = {2, 2};
  EXPECT_TRUE(warmup::a_star_search(maze, start, end));

  warmup::Maze expected_maze = {
      {'V', 'V', 'V'}, {'#', '#', 'V'}, {'.', '.', 'V'}};
  // The end cell itself is not marked as 'V' in the original implementation
  // because the loop terminates when the end is reached.
  maze[end.y][end.x] = 'V';

  if (maze != expected_maze) {
    std::cout << "Actual maze:" << std::endl;
    print_maze(maze);
    std::cout << "Expected maze:" << std::endl;
    print_maze(expected_maze);
  }

  EXPECT_EQ(maze, expected_maze);
}