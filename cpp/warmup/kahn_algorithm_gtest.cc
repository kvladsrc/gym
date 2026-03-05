#include <gtest/gtest.h>

#include <cstddef>
#include <vector>

#include "cpp/warmup/kahn_algorithm.hpp"

// Helper: checks that 'order' is a valid topological ordering for graph 'g'.
// Returns true if for every edge (u -> v), pos[u] < pos[v] in 'order'.
bool isValidTopologicalOrder(const warmup::graph& g,
                              const std::vector<std::size_t>& order) {
  if (order.size() != g.size()) {
    return false;
  }

  // Build position map: pos[vertex] = index in order.
  std::vector<std::size_t> pos(g.size());
  for (std::size_t i = 0; i < order.size(); ++i) {
    pos[order[i]] = i;
  }

  for (std::size_t u = 0; u < g.size(); ++u) {
    for (auto v : g[u]) {
      if (pos[u] >= pos[v]) {
        return false;
      }
    }
  }
  return true;
}

TEST(KahnAlgorithmTest, EmptyGraph) {
  warmup::graph g(0);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  EXPECT_TRUE(order.empty());
}

TEST(KahnAlgorithmTest, SingleVertex) {
  warmup::graph g(1);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  ASSERT_EQ(order.size(), 1u);
  EXPECT_EQ(order[0], 0u);
}

TEST(KahnAlgorithmTest, TwoVerticesWithEdge0to1) {
  warmup::graph g(2);
  g[0].push_back(1);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  ASSERT_EQ(order.size(), 2u);
  EXPECT_TRUE(isValidTopologicalOrder(g, order));
  // 0 must come before 1
  EXPECT_EQ(order[0], 0u);
  EXPECT_EQ(order[1], 1u);
}

TEST(KahnAlgorithmTest, TwoVerticesWithEdge1to0) {
  warmup::graph g(2);
  g[1].push_back(0);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  ASSERT_EQ(order.size(), 2u);
  EXPECT_TRUE(isValidTopologicalOrder(g, order));
  // 1 must come before 0
  EXPECT_EQ(order[0], 1u);
  EXPECT_EQ(order[1], 0u);
}

TEST(KahnAlgorithmTest, LinearChain) {
  // 0 -> 1 -> 2 -> 3
  warmup::graph g(4);
  g[0].push_back(1);
  g[1].push_back(2);
  g[2].push_back(3);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  ASSERT_EQ(order.size(), 4u);
  EXPECT_TRUE(isValidTopologicalOrder(g, order));
  // Only one valid topological order exists for a chain
  EXPECT_EQ(order[0], 0u);
  EXPECT_EQ(order[1], 1u);
  EXPECT_EQ(order[2], 2u);
  EXPECT_EQ(order[3], 3u);
}

TEST(KahnAlgorithmTest, DisconnectedGraphWithTwoChains) {
  // 0 -> 1   and   2 -> 3
  warmup::graph g(4);
  g[0].push_back(1);
  g[2].push_back(3);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  ASSERT_EQ(order.size(), 4u);
  EXPECT_TRUE(isValidTopologicalOrder(g, order));
}

TEST(KahnAlgorithmTest, BranchingGraph) {
  // 0 -> 1 -> 3
  // 0 -> 2 -> 3
  warmup::graph g(4);
  g[0].push_back(1);
  g[0].push_back(2);
  g[1].push_back(3);
  g[2].push_back(3);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  ASSERT_EQ(order.size(), 4u);
  EXPECT_TRUE(isValidTopologicalOrder(g, order));
  // 0 must be first, 3 must be last
  EXPECT_EQ(order[0], 0u);
  EXPECT_EQ(order[3], 3u);
}

TEST(KahnAlgorithmTest, MultipleSourceNodes) {
  // 0 -> 2 <- 1   (both 0 and 1 are sources, 2 depends on both)
  warmup::graph g(3);
  g[0].push_back(2);
  g[1].push_back(2);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  ASSERT_EQ(order.size(), 3u);
  EXPECT_TRUE(isValidTopologicalOrder(g, order));
  // 2 must be last
  EXPECT_EQ(order[2], 2u);
}

TEST(KahnAlgorithmTest, LongerDiamondGraph) {
  // 0 -> 1 -> 3 -> 5
  // 0 -> 2 -> 4 -> 5
  warmup::graph g(6);
  g[0].push_back(1);
  g[0].push_back(2);
  g[1].push_back(3);
  g[2].push_back(4);
  g[3].push_back(5);
  g[4].push_back(5);
  std::vector<std::size_t> order;
  warmup::topological_sort(g, order);
  ASSERT_EQ(order.size(), 6u);
  EXPECT_TRUE(isValidTopologicalOrder(g, order));
  EXPECT_EQ(order[0], 0u);
  EXPECT_EQ(order[5], 5u);
}
