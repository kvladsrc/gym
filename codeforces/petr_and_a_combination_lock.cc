#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct node {
  int val;
  std::unique_ptr<node> left;
  std::unique_ptr<node> right;
  node() : val(0), left(nullptr), right(nullptr) {}
};

void insert(std::unique_ptr<node> &root, int val, int p_val) {
  if (!root) {
    root = std::make_unique<node>();
    root->val = p_val + val;
  } else {
    insert(root->left, val, root->val);
    insert(root->right, -val, root->val);
  }
}

bool check(std::unique_ptr<node> &root) {
  if (!root->left && !root->right) {
    return (root->val % 360) == 0;
  } else {
    return check(root->left) || check(root->right);
  }
}

int main() {
  int n;
  std::cin >> n;
  auto root = std::make_unique<node>();
  root->val = 0;
  while (n--) {
    int buf;
    std::cin >> buf;
    insert(root, buf, 0);
  }

  std::cout << (check(root) ? "YES" : "NO") << "\n";

  return 0;
}
