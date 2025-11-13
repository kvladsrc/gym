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

int abs(int a) {
  if (a < 0) return -a;
  return a;
}

int pairs(vector<int> &boys, vector<int> &girls) {
  sort(girls.begin(), girls.end());
  sort(boys.begin(), boys.end());

  size_t g = 0, b = 0;
  int pairs = 0;
  while (g < girls.size() && b < boys.size()) {
    if (abs(girls[g] - boys[b]) <= 1) {
      g++;
      b++;
      pairs++;
      continue;
    }

    if (girls[g] > boys[b]) {
      b++;
    } else {
      g++;
    }
  }

  return pairs;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> boys(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> boys[i];
  }

  int m;
  std::cin >> m;
  vector<int> girls(m);
  for (int i = 0; i < m; ++i) {
    std::cin >> girls[i];
  }

  std::cout << pairs(boys, girls) << "\n";

  return 0;
}
