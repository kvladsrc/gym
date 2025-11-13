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
  int t;
  std::cin >> t;
  while (t--) {
    size_t n;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    // Let t is largest_deceasing_subsequence(a), then p(t) = 0. If it
    // optimal? We should proof, that s does not contain an element
    // which can be moved to t so p(s) + p(t) deceased. If it can be
    // added to t is should be in t since it is formed by LDS
    // algorithm.
    //
    // Does not works on {1 2 1 2}, t = {2 1}, s = {1 2}. Total
    // penalty is 1, optimal penalty is 0: t = {1 1}, s = {2 2}. 50
    // test case.
    //
    // Attempt 2. Greedy works!

    int res = 0;
    int group_t = -1;
    int group_s = -1;
    for (auto c : a) {
      if (group_t != -1 && group_s != -1 && group_t >= c && group_s >= c) {
        if (group_s > group_t) {
          group_t = c;
        } else {
          group_s = c;
        }

        continue;
      }

      if (group_t == -1 || group_t >= c) {
        group_t = c;
        continue;
      }

      if (group_s == -1 || group_s >= c) {
        group_s = c;
        continue;
      }

      res++;
      if (group_s > group_t) {
        group_t = c;
      } else {
        group_s = c;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
