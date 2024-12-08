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

const int64_t modulo = 1000000000 + 7;

vector<int64_t> char_segments(string &s, char c, char not_allowed) {
  vector<int64_t> res;
  int64_t cnt = 0;
  for (auto el : s) {
    if (el == c) {
      cnt++;
    } else {
      if (cnt) {
        res.push_back(cnt);
      }
      cnt = 0;
    }

    if (el == not_allowed) {
      return {0};
    }
  }
  if (cnt) {
    res.push_back(cnt);
  }

  return res;
}

int64_t num_messages(int64_t cnt, map<int64_t, int64_t> &table) {
  if (cnt <= 1) {
    return 1;
  }

  if (table[cnt]) {
    return table[cnt];
  }

  auto case1 = num_messages(cnt - 1, table);
  auto case2 = num_messages(cnt - 2, table);
  table[cnt] = (case1 + case2) % modulo;
  return table[cnt];
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string s;
  std::cin >> s;

  // Let's write a helper function f(x) -> number of unique messages
  // can be produced from x continious n or u chars. Another helper
  // function g(char, s) will produce vector of sizes of continious
  // substrings of char in s. Then final result is the product of the
  // numbers for all continious segments.
  int64_t res = 1;

  auto n_segs = char_segments(s, 'n', 'm');
  auto u_segs = char_segments(s, 'u', 'w');
  map<int64_t, int64_t> table;
  for (auto i : n_segs) {
    if (i == 0) {
      res = 0;
    }
    res *= num_messages(i, table);
    res %= modulo;
  }

  for (auto i : u_segs) {
    if (i == 0) {
      res = 0;
    }
    res *= num_messages(i, table);
    res %= modulo;
  }

  std::cout << res << "\n";

  return 0;
}
