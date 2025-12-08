#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/graph_heuristic_painting.hpp"

using warmup::graph;
using warmup::heuristic_painting;

TEST(GraphHeuristicPaintingTest, Empty) {
  int max_color = 0;
  auto res = heuristic_painting(0, {}, max_color);
  EXPECT_EQ(max_color, 0);
  EXPECT_TRUE(res.empty());
}

TEST(GraphHeuristicPaintingTest, SingleNode) {
  int max_color = 0;
  graph g(1);
  auto res = heuristic_painting(1, g, max_color);
  EXPECT_EQ(max_color, 1);
  EXPECT_EQ(res[0], 1);
}

TEST(GraphHeuristicPaintingTest, TwoNodesConnected) {
  int max_color = 0;
  graph g(2);
  g[0].push_back(1);
  g[1].push_back(0);

  auto res = heuristic_painting(2, g, max_color);
  EXPECT_EQ(max_color, 2);
  EXPECT_NE(res[0], res[1]);
}

TEST(GraphHeuristicPaintingTest, Triangle) {
  int max_color = 0;
  graph g(3);
  g[0] = {1, 2};
  g[1] = {0, 2};
  g[2] = {0, 1};

  auto res = heuristic_painting(3, g, max_color);
  EXPECT_EQ(max_color, 3);
  EXPECT_NE(res[0], res[1]);
  EXPECT_NE(res[0], res[2]);
  EXPECT_NE(res[1], res[2]);
}

TEST(GraphHeuristicPaintingTest, Bipartite) {
  int max_color = 0;
  graph g(4);
  // 0-1, 1-2, 2-3, 3-0 (Square)
  g[0] = {1, 3};
  g[1] = {0, 2};
  g[2] = {1, 3};
  g[3] = {2, 0};

  auto res = heuristic_painting(4, g, max_color);
  EXPECT_EQ(max_color, 2);
  EXPECT_EQ(res[0], res[2]);
  EXPECT_EQ(res[1], res[3]);
  EXPECT_NE(res[0], res[1]);
}
