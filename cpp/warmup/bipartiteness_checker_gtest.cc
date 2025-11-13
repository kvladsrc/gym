#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/bipartiteness_checker.hpp"

TEST(BipartitenessTest, EmptyGraph) {
  warmup::graph g(0);
  EXPECT_TRUE(warmup::bipartiteness_test(g));
}

TEST(BipartitenessTest, SingleVertex) {
  warmup::graph g(1);
  EXPECT_TRUE(warmup::bipartiteness_test(g));
}

TEST(BipartitenessTest, TwoVerticesWithEdge) {
  warmup::graph g(2);
  g[0].push_back(1);
  g[1].push_back(0);
  EXPECT_TRUE(warmup::bipartiteness_test(g));
}

TEST(BipartitenessTest, OddCycleTriangle) {
  warmup::graph g(3);
  g[0].push_back(1);
  g[1].push_back(0);
  g[1].push_back(2);
  g[2].push_back(1);
  g[2].push_back(0);
  g[0].push_back(2);
  EXPECT_FALSE(warmup::bipartiteness_test(g));
}

TEST(BipartitenessTest, EvenCycleSquare) {
  warmup::graph g(4);
  g[0].push_back(1);
  g[1].push_back(0);
  g[1].push_back(2);
  g[2].push_back(1);
  g[2].push_back(3);
  g[3].push_back(2);
  g[3].push_back(0);
  g[0].push_back(3);
  EXPECT_TRUE(warmup::bipartiteness_test(g));
}

TEST(BipartitenessTest, DisconnectedBipartiteGraph) {
  warmup::graph g(5);
  g[0].push_back(1);
  g[1].push_back(0);
  g[1].push_back(2);
  g[2].push_back(1);
  EXPECT_TRUE(warmup::bipartiteness_test(g));
}

TEST(BipartitenessTest, DisconnectedGraphWithNonBipartiteComponent) {
  warmup::graph g(5);
  g[0].push_back(1);
  g[1].push_back(0);
  g[1].push_back(2);
  g[2].push_back(1);
  g[2].push_back(0);
  g[0].push_back(2);
  g[3].push_back(4);
  g[4].push_back(3);
  EXPECT_FALSE(warmup::bipartiteness_test(g));
}
