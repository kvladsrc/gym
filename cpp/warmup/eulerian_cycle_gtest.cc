#include <gtest/gtest.h>

#include <algorithm>
#include <vector>

#include "cpp/warmup/eulerian_cycle.hpp"

TEST(EulerianCycleTest, EmptyGraph) {
  warmup::graph g(0);
  EXPECT_TRUE(warmup::eulerian_cycle_exists(g));
}

TEST(EulerianCycleTest, SingleVertex) {
  warmup::graph g(1);
  EXPECT_TRUE(warmup::eulerian_cycle_exists(g));
}

TEST(EulerianCycleTest, TwoVerticesWithOneEdge) {
  warmup::graph g(2);
  g[0].push_back(1);
  g[1].push_back(0);
  EXPECT_FALSE(warmup::eulerian_cycle_exists(g));
}

TEST(EulerianCycleTest, CompleteGraphWithThreeVertices) {
  warmup::graph g(3);
  g[0].push_back(1);
  g[1].push_back(0);
  g[1].push_back(2);
  g[2].push_back(1);
  g[2].push_back(0);
  g[0].push_back(2);
  EXPECT_TRUE(warmup::eulerian_cycle_exists(g));
}

TEST(EulerianCycleTest, DisconnectedGraph) {
  warmup::graph g(4);
  g[0].push_back(1);
  g[1].push_back(0);
  g[2].push_back(3);
  g[3].push_back(2);
  EXPECT_FALSE(warmup::eulerian_cycle_exists(g));
}

TEST(EulerianCycleTest, GraphWithOddDegreeVertices) {
  warmup::graph g(4);
  g[0].push_back(1);
  g[1].push_back(0);
  g[1].push_back(2);
  g[2].push_back(1);
  g[2].push_back(3);
  g[3].push_back(2);
  EXPECT_FALSE(warmup::eulerian_cycle_exists(g));
}

TEST(EulerianCycleTest, GraphWithEvenDegreesButDisconnected) {
  warmup::graph g(4);
  g[0].push_back(1);
  g[1].push_back(0);
  g[2].push_back(3);
  g[3].push_back(2);
  EXPECT_FALSE(warmup::eulerian_cycle_exists(g));
}

TEST(EulerianCycleTest, GraphWithDuplicatingEdges) {
  warmup::graph g(2);
  g[0].push_back(1);
  g[1].push_back(0);
  g[0].push_back(1);
  g[1].push_back(0);
  EXPECT_TRUE(warmup::eulerian_cycle_exists(g));
}

TEST(EulerianCycleTest, GraphWithIsolatedVertex) {
  warmup::graph g(3);
  g[0].push_back(1);
  g[1].push_back(0);
  EXPECT_FALSE(warmup::eulerian_cycle_exists(g));
}
