#include <gtest/gtest.h>

#include <algorithm>
#include <sstream>
#include <string>
#include <vector>

#include "cpp/warmup/graph_topological_sort.hpp"

// Helper function to convert rank vector to a formatted string
std::string rankVectorToString(const std::vector<int>& rank) {
  std::ostringstream oss;
  for (std::size_t i = 0; i < rank.size(); ++i) {
    oss << i << " " << rank[i] << "\n";
  }
  return oss.str();
}

TEST(GraphTopologicalSortTest, EmptyGraph) {
  warmup::graph g(0);
  std::vector<int> rank(0);
  warmup::topological_sort(g, rank);
  EXPECT_EQ(rankVectorToString(rank), "");
}

TEST(GraphTopologicalSortTest, SingleVertex) {
  warmup::graph g(1);
  std::vector<int> rank(1);
  warmup::topological_sort(g, rank);
  EXPECT_EQ(rankVectorToString(rank), "0 1\n");
}

TEST(GraphTopologicalSortTest, TwoVerticesWithEdge0to1) {
  warmup::graph g(2);
  g[0].push_back(1);
  std::vector<int> rank(2);
  warmup::topological_sort(g, rank);
  EXPECT_EQ(rankVectorToString(rank), "0 3\n1 2\n");
}

TEST(GraphTopologicalSortTest, TwoVerticesWithEdge1to0) {
  warmup::graph g(2);
  g[1].push_back(0);
  std::vector<int> rank(2);
  warmup::topological_sort(g, rank);
  EXPECT_EQ(rankVectorToString(rank), "0 1\n1 3\n");
}

TEST(GraphTopologicalSortTest, LinearChain) {
  warmup::graph g(4);
  g[0].push_back(1);
  g[1].push_back(2);
  g[2].push_back(3);
  std::vector<int> rank(4);
  warmup::topological_sort(g, rank);
  EXPECT_EQ(rankVectorToString(rank), "0 7\n1 6\n2 5\n3 4\n");
}

TEST(GraphTopologicalSortTest, DisconnectedGraphWithTwoChains) {
  warmup::graph g(4);
  g[0].push_back(1);
  g[2].push_back(3);
  std::vector<int> rank(4);
  warmup::topological_sort(g, rank);
  EXPECT_EQ(rankVectorToString(rank), "0 3\n1 2\n2 7\n3 6\n");
}

TEST(GraphTopologicalSortTest, BranchingGraph) {
  warmup::graph g(4);
  g[0].push_back(1);
  g[0].push_back(2);
  g[1].push_back(3);
  g[2].push_back(3);
  std::vector<int> rank(4);
  warmup::topological_sort(g, rank);
  EXPECT_EQ(rankVectorToString(rank), "0 7\n1 4\n2 6\n3 3\n");
}
