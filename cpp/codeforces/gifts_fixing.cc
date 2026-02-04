#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

void solve() {
  int n;
  std::cin >> n;
  vector<int64_t> candies(n);
  vector<int64_t> oranges(n);

  int64_t min_orange = 10000000000;
  int64_t min_candy = 10000000000;

  for (int i = 0; i < n; ++i) {
    std::cin >> candies[i];
    if (candies[i] < min_candy) min_candy = candies[i];
  }

  for (int i = 0; i < n; ++i) {
    std::cin >> oranges[i];
    if (oranges[i] < min_orange) min_orange = oranges[i];
  }

  int64_t res = 0;
  for (int i = 0; i < n; ++i) {
    int64_t moves = max(candies[i] - min_candy, oranges[i] - min_orange);
    res += moves;
  }

  std::cout << res << std::endl;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    solve();
  }
  return 0;
}
