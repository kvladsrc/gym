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

// The partial algorithm is simplest for array with duplicates among
// those that I saw, much easier to make a mistake on l++&r--
// algorithm in case of duplicates.
int partial(vector<int> &a, int l, int r) {
  int pivot = a[r];

  // It always increase the sep before swapping.
  int sep = l - 1;
  for (int i = l; i <= (r - 1); ++i) {
    if (a[i] <= pivot) {
      sep++;
      swap(a[sep], a[i]);
    }
  }

  // Important part!
  sep++;
  swap(a[sep], a[r]);

  return sep;
}

void print(vector<int> &a) {
  if (a.empty()) {
    return;
  }

  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";
}

void quick_sort(vector<int> &a, int l, int r) {
  if (l >= r) {
    return;
  }

  int pos = partial(a, l, r);
  quick_sort(a, l, pos - 1);
  quick_sort(a, pos + 1, r);
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  quick_sort(a, 0, n - 1);
  print(a);

  return 0;
}
