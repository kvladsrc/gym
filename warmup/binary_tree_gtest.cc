#include <gtest/gtest.h>

#include <sstream>
#include <string>

#include "cpp/warmup/binary_tree.hpp"

TEST(BinaryTreeTest, Empty) {
  warmup::bin_tree tree;
  EXPECT_EQ(tree.size, 0);
  testing::internal::CaptureStdout();
  tree.preorder();
  std::string output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "");
}

TEST(BinaryTreeTest, SingleNode) {
  warmup::bin_tree tree;
  tree.insert(1);
  EXPECT_EQ(tree.size, 1);
  testing::internal::CaptureStdout();
  tree.preorder();
  std::string output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 ");

  testing::internal::CaptureStdout();
  tree.inorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 ");

  testing::internal::CaptureStdout();
  tree.postorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 ");
}

TEST(BinaryTreeTest, TwoNodesRightChild) {
  warmup::bin_tree tree;
  tree.insert(1);
  tree.insert(2);
  EXPECT_EQ(tree.size, 2);

  testing::internal::CaptureStdout();
  tree.preorder();
  std::string output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 2 ");

  testing::internal::CaptureStdout();
  tree.inorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 2 ");

  testing::internal::CaptureStdout();
  tree.postorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "2 1 ");
}

TEST(BinaryTreeTest, ThreeNodesBalanced) {
  warmup::bin_tree tree;
  tree.insert(2);
  tree.insert(1);
  tree.insert(3);
  EXPECT_EQ(tree.size, 3);

  testing::internal::CaptureStdout();
  tree.preorder();
  std::string output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "2 1 3 ");

  testing::internal::CaptureStdout();
  tree.inorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 2 3 ");

  testing::internal::CaptureStdout();
  tree.postorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 3 2 ");
}

TEST(BinaryTreeTest, ThreeNodesLeftSkewed) {
  warmup::bin_tree tree;
  tree.insert(3);
  tree.insert(2);
  tree.insert(1);
  EXPECT_EQ(tree.size, 3);

  testing::internal::CaptureStdout();
  tree.preorder();
  std::string output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "3 2 1 ");

  testing::internal::CaptureStdout();
  tree.inorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 2 3 ");

  testing::internal::CaptureStdout();
  tree.postorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1 2 3 ");
}

TEST(BinaryTreeTest, FiveNodesMixed) {
  warmup::bin_tree tree;
  tree.insert(5);
  tree.insert(3);
  tree.insert(7);
  tree.insert(4);
  tree.insert(6);
  EXPECT_EQ(tree.size, 5);

  testing::internal::CaptureStdout();
  tree.preorder();
  std::string output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "5 3 4 7 6 ");

  testing::internal::CaptureStdout();
  tree.inorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "3 4 5 6 7 ");

  testing::internal::CaptureStdout();
  tree.postorder();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "4 3 6 7 5 ");
}
