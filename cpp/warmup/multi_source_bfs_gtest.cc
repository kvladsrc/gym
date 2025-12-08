#include <gtest/gtest.h>

#include <climits>
#include <vector>

#include "cpp/warmup/multi_source_bfs.hpp"

namespace warmup {

TEST(MultiSourceBFSTest, SimpleLineGraph) {
  // 0 -- 1 -- 2 -- 3
  // B    W    W    B
  graph g(4);
  g[0] = {1};
  g[1] = {0, 2};
  g[2] = {1, 3};
  g[3] = {2};

  std::vector<char> c = {'B', 'W', 'W', 'B'};
  std::vector<int> d(4);

  closest_blue(g, d, c);

  EXPECT_EQ(d[0], 0);
  EXPECT_EQ(d[1], 1);
  EXPECT_EQ(d[2], 1);
  EXPECT_EQ(d[3], 0);
}

TEST(MultiSourceBFSTest, DisconnectedComponents) {
  // 0 -- 1    2 -- 3
  // B    W    W    W
  graph g(4);
  g[0] = {1};
  g[1] = {0};
  g[2] = {3};
  g[3] = {2};

  std::vector<char> c = {'B', 'W', 'W', 'W'};
  std::vector<int> d(4);

  closest_blue(g, d, c);

  EXPECT_EQ(d[0], 0);
  EXPECT_EQ(d[1], 1);
  EXPECT_EQ(d[2], INT_MAX);
  EXPECT_EQ(d[3], INT_MAX);
}

TEST(MultiSourceBFSTest, NoBlueNodes) {
  // 0 -- 1
  // W    W
  graph g(2);
  g[0] = {1};
  g[1] = {0};

  std::vector<char> c = {'W', 'W'};
  std::vector<int> d(2);

  closest_blue(g, d, c);

  EXPECT_EQ(d[0], INT_MAX);
  EXPECT_EQ(d[1], INT_MAX);
}

TEST(MultiSourceBFSTest, AllBlueNodes) {
  // 0 -- 1
  // B    B
  graph g(2);
  g[0] = {1};
  g[1] = {0};

  std::vector<char> c = {'B', 'B'};
  std::vector<int> d(2);

  closest_blue(g, d, c);

  EXPECT_EQ(d[0], 0);
  EXPECT_EQ(d[1], 0);
}

TEST(MultiSourceBFSTest, CycleGraph) {
  // 0 -- 1
  // |    |
  // 3 -- 2
  // B at 0
  graph g(4);
  g[0] = {1, 3};
  g[1] = {0, 2};
  g[2] = {1, 3};
  g[3] = {0, 2};

  std::vector<char> c = {'B', 'W', 'W', 'W'};
  std::vector<int> d(4);

  closest_blue(g, d, c);

  EXPECT_EQ(d[0], 0);
  EXPECT_EQ(d[1], 1);
  EXPECT_EQ(d[2], 2);
  EXPECT_EQ(d[3], 1);
}

}  // namespace warmup
