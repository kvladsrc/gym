#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/sets_in_tree.hpp"

using std::vector;
using warmup::graph;
using warmup::solve;

TEST(SetsInTreeTest, Basic) {
  // 4 vertices
  // colors: 1 2 3 4
  // edges: 1-2, 2-3, 2-4
  // 0-indexed:
  // colors: 1 2 3 4 (indices 0, 1, 2, 3)
  // edges: 0-1, 1-2, 1-3

  int n = 4;
  vector<int64_t> colors = {1, 2, 3, 4};
  graph g(n);
  g[0] = {1};
  g[1] = {0, 2, 3};
  g[2] = {1};
  g[3] = {1};

  vector<int64_t> res(n);
  delete solve(g, res, colors, 0, -1);

  // Subtrees:
  // 2 (leaf): color {3}, max_freq 1, sum 3
  // 3 (leaf): color {4}, max_freq 1, sum 4
  // 1: children 2, 3. colors {2, 3, 4}. max_freq 1. sum 2+3+4 = 9.
  // 0: child 1. colors {1, 2, 3, 4}. max_freq 1. sum 1+2+3+4 = 10.

  EXPECT_EQ(res[0], 10);
  EXPECT_EQ(res[1], 9);
  EXPECT_EQ(res[2], 3);
  EXPECT_EQ(res[3], 4);
}

TEST(SetsInTreeTest, SameColors) {
  // 3 vertices, line 0-1-2
  // colors: 1 1 1
  int n = 3;
  vector<int64_t> colors = {1, 1, 1};
  graph g(n);
  g[0] = {1};
  g[1] = {0, 2};
  g[2] = {1};

  vector<int64_t> res(n);
  delete solve(g, res, colors, 0, -1);

  // 2: {1}, sum 1
  // 1: {1, 1}, sum 1
  // 0: {1, 1, 1}, sum 1

  EXPECT_EQ(res[0], 1);
  EXPECT_EQ(res[1], 1);
  EXPECT_EQ(res[2], 1);
}

TEST(SetsInTreeTest, SecondCase) {
  // 15 nodes
  // Colors: 1 2 3 1 2 3 3 1 1 3 2 2 1 2 3
  // Edges: 1-2, 1-3, 1-4, 1-14, 1-15, 2-5, 2-6, 2-7, 3-8, 3-9, 3-10, 4-11,
  // 4-12, 4-13 0-indexed edges: 0-1, 0-2, 0-3, 0-13, 0-14, 1-4, 1-5, 1-6, 2-7,
  // 2-8, 2-9, 3-10, 3-11, 3-12

  int n = 15;
  std::vector<int64_t> colors = {1, 2, 3, 1, 2, 3, 3, 1, 1, 3, 2, 2, 1, 2, 3};
  graph g(n);
  auto add_edge = [&](int u, int v) {
    u--;
    v--;
    g[u].push_back(v);
    g[v].push_back(u);
  };

  add_edge(1, 2);
  add_edge(1, 3);
  add_edge(1, 4);
  add_edge(1, 14);
  add_edge(1, 15);
  add_edge(2, 5);
  add_edge(2, 6);
  add_edge(2, 7);
  add_edge(3, 8);
  add_edge(3, 9);
  add_edge(3, 10);
  add_edge(4, 11);
  add_edge(4, 12);
  add_edge(4, 13);

  vector<int64_t> res(n);
  delete solve(g, res, colors, 0, -1);

  // Expected: 6 5 4 3 2 3 3 1 1 3 2 2 1 2 3
  std::vector<int64_t> expected = {6, 5, 4, 3, 2, 3, 3, 1, 1, 3, 2, 2, 1, 2, 3};

  ASSERT_EQ(res.size(), n);
  for (int i = 0; i < n; ++i) {
    EXPECT_EQ(res[i], expected[i]) << "Mismatch at index " << i;
  }
}
