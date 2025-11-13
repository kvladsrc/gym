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
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

// Number of incert or remove operations to make a equal to b;
int edit_distance(string const &a, string const &b) {
  size_t b_idx = 0, a_idx = 0;
  int res = 0;
  while (b_idx < b.size() && a_idx < a.size()) {
    if (a[a_idx] == b[b_idx]) {
      a_idx++;
      b_idx++;
      continue;
    }

    res++;
    a_idx++;
  }

  res += a.size() - a_idx;
  res += b.size() - b_idx;
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;

  vector<string> powers;
  uint64_t power = 1;
  for (int i = 0; i < 64; ++i) {
    powers.push_back(std::to_string(power));
    power *= 2;
  }

  while (t--) {
    string s;
    std::cin >> s;

    int res = 100;
    for (auto p : powers) {
      auto local = edit_distance(s, p);
      res = min(res, local);
    }

    std::cout << res << "\n";
  }

  return 0;
}
