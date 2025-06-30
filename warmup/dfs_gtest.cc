#include <gtest/gtest.h>

#include "cpp/warmup/dfs.hpp"

TEST(DFSTest, EmptyGraph) {
  warmup::graph g(0);
  std::vector<int> cc_table(0);
  // No-op, but shouldn't crash
  // warmup::cc_dfs(g, 0, cc_table, 1);
  EXPECT_TRUE(cc_table.empty());
}

TEST(DFSTest, SingleVertex) {
  warmup::graph g(1);
  std::vector<int> cc_table(1, 0);
  warmup::cc_dfs(g, 0, cc_table, 1);
  EXPECT_EQ(cc_table, std::vector<int>({1}));
}

TEST(DFSTest, TwoVerticesConnected) {
  warmup::graph g(2);
  g[0].push_back(1);
  g[1].push_back(0);
  std::vector<int> cc_table(2, 0);
  warmup::cc_dfs(g, 0, cc_table, 1);
  EXPECT_EQ(cc_table, std::vector<int>({1, 1}));
}

TEST(DFSTest, TwoVerticesDisconnected) {
  warmup::graph g(2);
  std::vector<int> cc_table(2, 0);
  warmup::cc_dfs(g, 0, cc_table, 1);
  EXPECT_EQ(cc_table, std::vector<int>({1, 0}));
}

TEST(DFSTest, ThreeVerticesLine) {
  warmup::graph g(3);
  g[0].push_back(1);
  g[1].push_back(0);
  g[1].push_back(2);
  g[2].push_back(1);
  std::vector<int> cc_table(3, 0);
  warmup::cc_dfs(g, 0, cc_table, 1);
  EXPECT_EQ(cc_table, std::vector<int>({1, 1, 1}));
}
