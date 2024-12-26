#include <algorithm>
#include <climits>
#include <iostream>
#include <string>
#include <vector>

using std::min;
using std::string;
using std::vector;

static int min_rec(vector<int>::iterator start, vector<int>::iterator end) {
  if (start == end) {
    return INT_MAX;
  }

  return min(*start, min_rec(start + 1, end));
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  std::cout << min_rec(a.begin(), a.end()) << "\n";

  return 0;
}
