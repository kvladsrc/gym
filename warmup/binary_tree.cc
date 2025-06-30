#include "cpp/warmup/binary_tree.hpp"

#include <iostream>

namespace warmup {

// node method implementations
node::node() : val(0), left(nullptr), right(nullptr) {}

node::node(int aval) : val(aval), left(nullptr), right(nullptr) {}

node::~node() {
  delete left;
  delete right;
}

bool node::search(int aval) const {
  if (val == aval) {
    return true;
  }

  if (aval > val) {
    if (right == nullptr) {
      return false;
    }

    return right->search(aval);
  }

  if (left == nullptr) {
    return false;
  }

  return left->search(aval);
}

void node::insert(int aval) {
  if (aval == val) {
    return;
  }

  if (aval > val) {
    if (right == nullptr) {
      right = new node(aval);
      return;
    }

    right->insert(aval);
    return;
  }

  if (left == nullptr) {
    left = new node(aval);
    return;
  }

  left->insert(aval);
}

void node::preorder() const {
  std::cout << val << " ";
  if (left != nullptr) {
    left->preorder();
  }
  if (right != nullptr) {
    right->preorder();
  }
}

void node::inorder() const {
  if (left != nullptr) {
    left->inorder();
  }
  std::cout << val << " ";
  if (right != nullptr) {
    right->inorder();
  }
}

void node::postorder() const {
  if (left != nullptr) {
    left->postorder();
  }
  if (right != nullptr) {
    right->postorder();
  }
  std::cout << val << " ";
}

// bin_tree method implementations
bin_tree::bin_tree() : root(nullptr), size(0) {}

bin_tree::~bin_tree() { delete root; }

void bin_tree::insert(int aval) {
  if (size == 0) {
    root = new node(aval);
    size++;
    return;
  }

  if (root->search(aval)) {
    return;
  }

  root->insert(aval);
  size++;
}

bool bin_tree::search(int aval) const {
  if (size == 0) {
    return false;
  }

  return root->search(aval);
}

void bin_tree::preorder() const {
  if (size == 0) {
    return;
  }
  root->preorder();
}

void bin_tree::inorder() const {
  if (size == 0) {
    return;
  }
  root->inorder();
}

void bin_tree::postorder() const {
  if (size == 0) {
    return;
  }
  root->postorder();
}

}  // namespace warmup
