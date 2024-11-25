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
using std::gcd;
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

bool cmp(const pair<int, int> &a, const pair<int, int> &b) {
  return a.first < b.first;
}

int main() {
  int n;
  std::cin >> n;

  if (n == 0) {
    std::cout << 0 << std::endl;
    return 0;
  }

  vector<pair<int, int>> trees(n);
  for (auto &tree : trees) {
    std::cin >> tree.first >> tree.second;
  }

  // Sort trees by their positions
  sort(trees.begin(), trees.end(), cmp);

  int last_position = trees[0].first - trees[0].second -
                      1;  // Initialize to a position far left of the first tree
  int felled_trees = 0;

  for (int i = 0; i < n; ++i) {
    auto [pos, height] = trees[i];
    if ((pos - height) > last_position) {
      // Fell to the left
      felled_trees++;
      last_position = pos;
    } else if (i < n - 1 && (pos + height) < trees[i + 1].first) {
      // Fell to the right
      felled_trees++;
      last_position = pos + height;
    } else if (i == n - 1) {
      // Last tree can always be felled
      felled_trees++;
    } else {
      // Keep the tree standing
      last_position = pos;
    }
  }

  std::cout << felled_trees << std::endl;

  return 0;
}
