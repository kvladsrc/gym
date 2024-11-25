#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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

int main(int argc, char *argv[]) {
  int n, k;
  std::cin >> n >> k;
  string s;
  std::cin >> s;
  unordered_map<char, bool> keyboard;
  for (int i = 0; i < k; ++i) {
    char c;
    std::cin >> c;
    keyboard[c] = true;
  }

  vector<int64_t> prefs;
  int cur = 0;
  for (char c : s) {
    if (keyboard[c]) {
      cur++;
    } else {
      prefs.push_back(cur);
      cur = 0;
    }
  }
  if (cur) prefs.push_back(cur);

  int64_t res = 0;
  for (auto i : prefs) {
    // i + (i - 1) + (i - 2) + 1
    // sum(1..i) := (i + 1)i / 2
    res += ((i + 1) * i) / 2;
  }

  std::cout << res << "\n";

  return 0;
}
