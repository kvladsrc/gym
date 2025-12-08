#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/ford_bellman.hpp"

using std::vector;
using warmup::ford_bellman;

const int INF = 1000000000;

TEST(FordBellmanTest, Basic) {
  int n = 3;
  vector<vector<int>> graph(n, vector<int>(n, INF));
  // 0->1 (10), 1->2 (20), 0->2 (50)
  graph[0][1] = 10;
  graph[1][2] = 20;
  graph[0][2] = 50;

  auto dists = ford_bellman(n, graph);

  EXPECT_EQ(dists[0], 0);
  EXPECT_EQ(dists[1], 10);
  EXPECT_EQ(dists[2], 30);  // 0->1->2 is shorter than 0->2
}

TEST(FordBellmanTest, NoPath) {
  int n = 3;
  vector<vector<int>> graph(n, vector<int>(n, INF));
  // 0->1 (10)
  graph[0][1] = 10;

  auto dists = ford_bellman(n, graph);

  EXPECT_EQ(dists[0], 0);
  EXPECT_EQ(dists[1], 10);
  EXPECT_EQ(dists[2], INF);
}

TEST(FordBellmanTest, NegativeWeight) {
  int n = 3;
  vector<vector<int>> graph(n, vector<int>(n, INF));
  // 0->1 (10), 1->2 (-5)
  graph[0][1] = 10;
  graph[1][2] = -5;

  auto dists = ford_bellman(n, graph);

  EXPECT_EQ(dists[0], 0);
  EXPECT_EQ(dists[1], 10);
  EXPECT_EQ(dists[2], 5);
}
