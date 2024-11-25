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
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    int mins_size = (n * (n - 1)) / 2;
    vector<int> mins(mins_size);
    for (auto &i : mins) {
      std::cin >> i;
    }

    sort(mins.begin(), mins.end());

    vector<int> res(n);
    res[0] = mins.back();
    for (int i = 1; i < n; ++i) {
      res[i] = mins.back();
      for (int j = 0; j < i; ++j) {
        mins.pop_back();
      }
    }

    for (auto i : res) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
