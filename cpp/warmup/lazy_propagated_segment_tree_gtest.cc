#include <gtest/gtest.h>

#include "cpp/warmup/lazy_propagated_segment_tree.hpp"

using warmup::LazySegmentTree;

TEST(LazySegmentTreeTest, Basic) {
  int n = 5;
  LazySegmentTree st(n);
  // Init with all 0
  st.build({0, 0, 0, 0, 0});

  // Update range [1, 3] (0-indexed: indices 1, 2, 3) -> flip 0 to 1
  st.update(1, 3);
  // Array: 0 1 1 1 0

  EXPECT_EQ(st.query(0, 4), 3);
  EXPECT_EQ(st.query(1, 3), 3);
  EXPECT_EQ(st.query(0, 0), 0);
  EXPECT_EQ(st.query(4, 4), 0);

  // Update range [2, 4] -> flip
  // Array was: 0 1 1 1 0
  // Indices 2,3,4 flipped:
  // 2: 1->0
  // 3: 1->0
  // 4: 0->1
  // New array: 0 1 0 0 1
  st.update(2, 4);

  EXPECT_EQ(st.query(0, 4), 2);
  EXPECT_EQ(st.query(0, 1), 1);
  EXPECT_EQ(st.query(2, 3), 0);
  EXPECT_EQ(st.query(4, 4), 1);
}
