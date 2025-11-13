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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n), b(n);
    for (auto &i : a) {
      std::cin >> i;
    }
    for (auto &i : b) {
      std::cin >> i;
    }

    // Idea. Count the number of i such as a_i == b_i == -1 and j
    // where a_j == b_j == 1. Othere cases are -1&0, -1&1, 0&0 are
    // trivial to resolve by greedy alrogithm. Then we find optimal
    // useage of 1&1 and -1&-1 cases.
    int first = 0, second = 0;
    int both_pos = 0, both_neg = 0;
    for (int i = 0; i < n; ++i) {
      if (a[i] == -1 && b[i] == -1) {
        both_neg++;
        continue;
      }
      if (a[i] == 1 && b[i] == 1) {
        both_pos++;
        continue;
      }

      if (a[i] > b[i]) {
        first += a[i];
      } else {
        second += b[i];
      }
    }

    int hscore = max(first, second);
    int lscore = min(first, second);
    int delta = hscore - lscore;

    lscore += min(delta, both_pos);
    both_pos -= min(delta, both_pos);
    hscore += both_pos / 2;
    lscore += both_pos / 2;
    if (both_pos % 2) {
      hscore++;
    }

    delta = hscore - lscore;
    hscore -= min(delta, both_neg);
    both_neg -= min(delta, both_neg);
    hscore -= both_neg / 2;
    lscore -= both_neg / 2;
    if (both_neg % 2) {
      lscore--;
    }

    std::cout << min(lscore, hscore) << "\n";
  }

  return 0;
}
