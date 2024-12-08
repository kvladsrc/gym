#include <algorithm>
#include <climits>
#include <cmath>
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
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

vector<int> k_lcm_easy(int n) {
  vector<int> res;
  if (n % 4 == 0) {
    res.push_back(n / 2);
    res.push_back(n / 4);
    res.push_back(n / 4);
    return res;
  }

  if (n % 2 == 1) {
    res.push_back(n / 2);
    res.push_back(n / 2);
    res.push_back(1);
    return res;
  }

  res.push_back((n / 2) - 1);
  res.push_back((n / 2) - 1);
  res.push_back(2);
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    vector<int> res;
    while (k > 3) {
      res.push_back(1);
      k--;
      n--;
    }

    auto easy = k_lcm_easy(n);
    for (auto i : easy) {
      res.push_back(i);
    }

    for (auto i : res) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
