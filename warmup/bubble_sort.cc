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

void swap(int &a, int &b) {
  int buf = a;
  a = b;
  b = buf;
}

void bubble_sort(vector<int> &a) {
  while (true) {
    bool swapped = false;

    for (size_t i = 1; i < a.size(); ++i) {
      if (a[i - 1] > a[i]) {
        swap(a[i - 1], a[i]);
        swapped = true;
      }
    }

    if (!swapped)
      break;
  }
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  bubble_sort(a);

  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
