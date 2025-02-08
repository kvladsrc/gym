#include <cstddef>
#include <iostream>
#include <string>
#include <utility>
#include <vector>

using std::string;
using std::vector;

static int partition_to_right(vector<int> &a) {
  if (a.empty()) {
    return -1;
  }

  int const pivot = a.back();
  int less = -1;
  size_t more = 0;
  while (more < a.size() - 1) {
    if (a[more] < pivot) {
      std::swap(a[++less], a[more]);
    }

    more++;
  }
  std::swap(a[++less], a.back());

  return less;
}

static int partition_to_mid(vector<int> &a) {
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

  auto a_cpy = a;
  auto pivot_to_mid = partition_to_mid(a);
  auto pivot_to_right = partition_to_right(a_cpy);
  bool same = true;

  if (pivot_to_mid != pivot_to_right) {
    same = false;
  }

  std::cout << pivot_to_mid << " ";
  for (size_t idx = 0; idx < a.size(); ++idx) {
    if (a[idx] != a_cpy[idx]) {
      same = false;
    }
    std::cout << a[idx] << " ";
  }
  std::cout << (same ? "YES" : "NO") << "\n";

  return 0;
}
