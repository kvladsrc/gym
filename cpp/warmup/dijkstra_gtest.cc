#include <gtest/gtest.h>

#include "cpp/warmup/dijkstra.hpp"

TEST(DijkstraTest, EmptyGraph) {
  warmup::graph g(0);
  // No-op, but shouldn't crash
  // EXPECT_EQ(warmup::dijkstra(g, 0, 0), INT_MAX);
  EXPECT_TRUE(g.empty());
}

TEST(DijkstraTest, SingleVertex) {
  warmup::graph g(1);
  EXPECT_EQ(warmup::dijkstra(g, 0, 0), 0);
}

TEST(DijkstraTest, TwoVerticesConnected) {
  warmup::graph g(2);
  g[0].push_back({1, 10});
  g[1].push_back({0, 10});
  EXPECT_EQ(warmup::dijkstra(g, 0, 1), 10);
}

TEST(DijkstraTest, TwoVerticesDisconnected) {
  warmup::graph g(2);
  EXPECT_EQ(warmup::dijkstra(g, 0, 1), INT_MAX);
}

TEST(DijkstraTest, ThreeVerticesLine) {
  warmup::graph g(3);
  g[0].push_back({1, 10});
  g[1].push_back({0, 10});
  g[1].push_back({2, 20});
  g[2].push_back({1, 20});
  EXPECT_EQ(warmup::dijkstra(g, 0, 2), 30);
}

TEST(DijkstraTest, ThreeVerticesTriangle) {
  warmup::graph g(3);
  g[0].push_back({1, 10});
  g[1].push_back({0, 10});
  g[1].push_back({2, 20});
  g[2].push_back({1, 20});
  g[0].push_back({2, 100});
  g[2].push_back({0, 100});
  EXPECT_EQ(warmup::dijkstra(g, 0, 2), 30);
}
