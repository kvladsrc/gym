#include <algorithm>
#include <climits>
#include <cmath>
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
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> rems(10, 0);
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      buf %= 10;
      rems[buf]++;
    }

    bool res = false;
    for (int i = 0; i < 10; ++i) {
      for (int j = 0; j < 10; ++j) {
        if (rems[i] == 0 || rems[j] == 0) {
          continue;
        }

        if (i == j && rems[i] < 2) {
          continue;
        }

        int have = i + j;
        have %= 10;
        int need = 13 - have;
        need %= 10;

        if (rems[need] < 1) {
          continue;
        }

        if (need == j && rems[j] < 2) {
          continue;
        }

        if (need == i && rems[i] < 2) {
          continue;
        }

        if (need == i && need == j && rems[need] < 3) {
          continue;
        }

        res = true;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
