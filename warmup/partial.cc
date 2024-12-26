#include <iostream>
#include <string>
#include <utility>
#include <vector>

using std::string;
using std::vector;

static int partial(vector<int> &a) {
  if (a.empty()) {
    return -1;
  }

  auto pivot = a.back();
  int l = 0;
  int r = a.size() - 2;
  while (l <= r) {
    if (a[l] < pivot) {
      l++;
      continue;
    }
    if (a[r] >= pivot) {
      r--;
      continue;
    }
    std::swap(a[l], a[r]);
  }

  std::swap(a[l], a.back());
  return l;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  auto pivot = partial(a);
  std::cout << pivot << '\n';
  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << '\n';

  return 0;
}
