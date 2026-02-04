#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct node {
  char c{};
  map<char, node*> children;
};

int count_nodes(const node* root) {
  int res = 1;

  res += std::accumulate(
      root->children.begin(), root->children.end(), 0,
      [](int acc, const auto& p) { return acc + count_nodes(p.second); });

  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string s;
  std::cin >> s;

  string mask;
  std::cin >> mask;

  int k = 0;
  std::cin >> k;

  auto* root = new node;
  root->c = 'R';

  for (size_t idx = 0; idx < s.size(); ++idx) {
    int bad = 0;
    auto* n = root;

    for (size_t cur = idx; cur < s.size(); ++cur) {
      auto c = s[cur];

      if (mask[c - 'a'] == '0') {
        bad++;
      }

      if (bad > k) {
        break;
      }

      auto [it, inserted] = n->children.try_emplace(c, nullptr);
      if (inserted) {
        it->second = new node;
        it->second->c = c;
      }

      n = n->children[c];
    }
  }

  auto res = count_nodes(root);
  std::cout << res - 1 << "\n";

  return 0;
}
