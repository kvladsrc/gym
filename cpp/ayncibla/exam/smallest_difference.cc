#include <algorithm>
#include <iostream>
#include <vector>

using std::vector;

int abs(int a) {
  if (a < 0) return -a;
  return a;
}

int smallest_diff(vector<int>& first, vector<int>& second) {
  std::sort(first.begin(), first.end());
  std::sort(second.begin(), second.end());

  int i = 0;
  int j = 0;
  int res = abs(first[i] - second[j]);

  while (true) {
    if (res > abs(first[i] - second[j])) {
      res = abs(first[i] - second[j]);
    }

    if (first[i] < second[j]) {
      if (i + 1 == first.size()) break;
      i++;
    } else {
      if (j + 1 == second.size()) break;
      j++;
    }
  }

  return res;
}

int main() {
  int n;
  std::cin >> n;
  vector<int> first(n);
  for (size_t j = 0; j < n; ++j) {
    std::cin >> first[j];
  }

  int m;
  std::cin >> m;
  vector<int> second(m);
  for (size_t j = 0; j < m; ++j) {
    std::cin >> second[j];
  }

  std::cout << smallest_diff(first, second) << "\n";
}
