#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <utility>
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

bool compare_laptops(const pair<int, int> &a, const pair<int, int> &b) {
  return a.first < b.first;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<pair<int, int>> laptops(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> laptops[i].first;
    std::cin >> laptops[i].second;
  }
  sort(laptops.begin(), laptops.end(), compare_laptops);

  bool fine = true;
  for (int i = 1; i < n; ++i) {
    if (laptops[i].first == laptops[i - 1].first) {
      laptops[i].second = max(laptops[i].second, laptops[i - 1].second);
      continue;
    }

    if (laptops[i].second < laptops[i - 1].second) {
      fine = false;
      break;
    }
  }

  if (fine) {
    std::cout << "Poor Alex"
              << "\n";
  } else {
    std::cout << "Happy Alex"
              << "\n";
  }

  return 0;
}
