#include <vector>

#include "centroid.hpp"
#include "gtest/gtest.h"

TEST(CentroidTest, SingleNode) {
  warmup::graph g(1);
  std::vector<int> sizes(1, -1);
  warmup::fill_sizes(g, sizes, 0, 0);
  EXPECT_EQ(warmup::centroid(g, sizes, 0, 0), 0);
}

TEST(CentroidTest, TwoNodes) {
  warmup::graph g(2);
  g[0].push_back(1);
  g[1].push_back(0);
  std::vector<int> sizes(2, -1);
  warmup::fill_sizes(g, sizes, 0, 0);
  // Both 0 and 1 are valid centroids, but our implementation should return one
  // of them consistently. Based on the implementation (greedy descent), it
  // starts at 0. sizes[1] will be 1. N/2 = 1. sizes[1] is not > 1. So it should
  // stay at 0.
  EXPECT_EQ(warmup::centroid(g, sizes, 0, 0), 0);
}

TEST(CentroidTest, StarGraph) {
  // Center 0, leaves 1, 2, 3, 4
  int n = 5;
  warmup::graph g(n);
  for (int i = 1; i < n; ++i) {
    g[0].push_back(i);
    g[i].push_back(0);
  }
  std::vector<int> sizes(n, -1);
  warmup::fill_sizes(g, sizes, 0, 0);
  EXPECT_EQ(warmup::centroid(g, sizes, 0, 0), 0);
}

TEST(CentroidTest, LineGraph) {
  // 0-1-2-3-4
  int n = 5;
  warmup::graph g(n);
  for (int i = 0; i < n - 1; ++i) {
    g[i].push_back(i + 1);
    g[i + 1].push_back(i);
  }
  std::vector<int> sizes(n, -1);
  warmup::fill_sizes(g, sizes, 0, 0);
  EXPECT_EQ(warmup::centroid(g, sizes, 0, 0), 2);
}

TEST(CentroidTest, TreeWithKnownCentroid) {
  //      0
  //     / \
  //    1   2
  //       / \
  //      3   4
  // Centroid should be 2.
  // Sizes:
  // 3: 1
  // 4: 1
  // 2: 3
  // 1: 1
  // 0: 5
  // Path: 0 -> 2 (size 3 > 2) -> stop (children 3, 4 have size 1 <= 2)
  int n = 5;
  warmup::graph g(n);
  g[0].push_back(1);
  g[1].push_back(0);
  g[0].push_back(2);
  g[2].push_back(0);
  g[2].push_back(3);
  g[3].push_back(2);
  g[2].push_back(4);
  g[4].push_back(2);

  std::vector<int> sizes(n, -1);
  warmup::fill_sizes(g, sizes, 0, 0);
  EXPECT_EQ(warmup::centroid(g, sizes, 0, 0), 2);
}
