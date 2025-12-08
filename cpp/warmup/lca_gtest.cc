#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/lca.hpp"

TEST(LCATest, EmptyGraph) {
  warmup::graph g;
  warmup::lca solver(g);
  EXPECT_EQ(solver.find(0, 0), 0);
}

TEST(LCATest, SingleVertex) {
  warmup::graph g(1);
  warmup::lca solver(g);
  EXPECT_EQ(solver.find(0, 0), 0);
}

TEST(LCATest, TwoVertices) {
  warmup::graph g(2);
  g[0].push_back(1);
  g[1].push_back(0);
  warmup::lca solver(g);
  EXPECT_EQ(solver.find(0, 1), 0);
  EXPECT_EQ(solver.find(1, 0), 0);
}

TEST(LCATest, LinearGraph) {
  // 0-1-2-3-4
  warmup::graph g(5);
  for (int i = 0; i < 4; ++i) {
    g[i].push_back(i + 1);
    g[i + 1].push_back(i);
  }
  warmup::lca solver(g);
  EXPECT_EQ(solver.find(0, 4), 0);
  EXPECT_EQ(solver.find(2, 4), 2);
  EXPECT_EQ(solver.find(3, 4), 3);
}

TEST(LCATest, BinaryTree) {
  //      0
  //     / \
  //    1   2
  //   / \
  //  3   4
  warmup::graph g(5);
  g[0] = {1, 2};
  g[1] = {0, 3, 4};
  g[2] = {0};
  g[3] = {1};
  g[4] = {1};

  warmup::lca solver(g);
  EXPECT_EQ(solver.find(3, 4), 1);
  EXPECT_EQ(solver.find(3, 2), 0);
  EXPECT_EQ(solver.find(1, 2), 0);
  EXPECT_EQ(solver.find(0, 4), 0);
}

TEST(LCATest, NaryTree) {
  //      0
  //    / | \
  //   1  2  3
  //   |
  //   4
  warmup::graph g(5);
  g[0] = {1, 2, 3};
  g[1] = {0, 4};
  g[2] = {0};
  g[3] = {0};
  g[4] = {1};

  warmup::lca solver(g);
  EXPECT_EQ(solver.find(1, 2), 0);
  EXPECT_EQ(solver.find(2, 3), 0);
  EXPECT_EQ(solver.find(4, 2), 0);
  EXPECT_EQ(solver.find(4, 3), 0);
}

TEST(LCATest, SameNode) {
  warmup::graph g(3);
  g[0] = {1};
  g[1] = {0, 2};
  g[2] = {1};
  warmup::lca solver(g);
  EXPECT_EQ(solver.find(1, 1), 1);
  EXPECT_EQ(solver.find(2, 2), 2);
}

TEST(LCATest, DirectAncestor) {
  warmup::graph g(2);
  g[0] = {1};
  g[1] = {0};
  warmup::lca solver(g);
  EXPECT_EQ(solver.find(0, 1), 0);
}

TEST(LCATest, ComplexTree) {
  //        0
  //      /   \
  //     1     2
  //    / \   / \
  //   3   4 5   6
  //      /
  //     7
  warmup::graph g(8);
  g[0] = {1, 2};
  g[1] = {0, 3, 4};
  g[2] = {0, 5, 6};
  g[3] = {1};
  g[4] = {1, 7};
  g[5] = {2};
  g[6] = {2};
  g[7] = {4};

  warmup::lca solver(g);
  EXPECT_EQ(solver.find(3, 7), 1);
  EXPECT_EQ(solver.find(7, 5), 0);
  EXPECT_EQ(solver.find(5, 6), 2);
  EXPECT_EQ(solver.find(3, 4), 1);
}

TEST(LCATest, OutOfBounds) {
  warmup::graph g(1);
  warmup::lca solver(g);
  EXPECT_EQ(solver.find(0, 100), 0);
  EXPECT_EQ(solver.find(100, 0), 0);
  EXPECT_EQ(solver.find(100, 100), 0);
}
