#include <algorithm>
#include <boost/range/algorithm/sort.hpp>
#include <cmath>
#include <cstddef>
#include <cstdlib>
#include <iostream>
#include <string>
#include <vector>

using std::min;
using std::pair;
using std::string;
using std::vector;

struct Point {
  int x;
  int y;

  double distance(Point const& other) const {
    return sqrt(((x - other.x) * (x - other.x)) +
                ((y - other.y) * (y - other.y)));
  }
};

static bool compare_points_x(Point const& a, const Point& b) {
  return a.x < b.x;
}

static bool compare_points_y(Point const& a, Point const& b) {
  return a.y < b.y;
}

static double minimal_distance_naive(const vector<Point>& points, size_t start,
                                     size_t end) {
  double min_dist = points[start].distance(points[end]);

  for (size_t i = start; i <= end; ++i) {
    for (size_t j = i + 1; j <= end; ++j) {
      auto tmp_dist = points[i].distance(points[j]);
      min_dist = std::min(tmp_dist, min_dist);
    }
  }

  return min_dist;
}

static double minimal_distance_fast(vector<Point>& points, size_t start,
                                    size_t end) {
  if ((end - start) == 1) {
    return points[start].distance(points[end]);
  }
  size_t const mid = (start + end) / 2;
  double min_dist = min(minimal_distance_fast(points, start, mid),
                        minimal_distance_fast(points, mid, end));

  size_t left = mid;
  size_t right = mid;
  auto mid_x = points[mid].x;

  while (left > start && mid_x - points[left].x <= min_dist) {
    left--;
  }

  while (right < end && points[right].x - mid_x <= min_dist) {
    right++;
  }

  if (left == mid || right == mid) {
    return min_dist;
  }

  vector<Point> vert_sorted(right - left + 1);
  for (size_t i = 0; i < vert_sorted.size(); ++i) {
    vert_sorted[i] = points[left + i];
  }

  boost::range::sort(vert_sorted, compare_points_y);

  for (size_t i = 0; i < vert_sorted.size(); ++i) {
    for (size_t j = i + 1; (j <= i + 7) && (j < vert_sorted.size()); ++j) {
      double const tmp_dist = vert_sorted[i].distance(vert_sorted[j]);
      min_dist = std::min(tmp_dist, min_dist);
    }
  }

  return min_dist;
}

static bool stress_test(unsigned int* seed) {
  size_t const n = (rand_r(seed) % 1000) + 2;

  vector<Point> fixed_x(n);
  vector<Point> fixed_y(n);
  vector<Point> points(n);

  int const x = ((rand_r(seed) % 2001) - 1000);
  int const y = ((rand_r(seed) % 2001) - 1000);

  for (size_t i = 0; i < n; i++) {
    points[i].x = (rand_r(seed) % 2001 - 1000);
    points[i].y = (rand_r(seed) % 2001 - 1000);

    fixed_x[i].y = (rand_r(seed) % 2001 - 1000);
    fixed_x[i].x = x;

    fixed_y[i].y = y;
    fixed_y[i].x = (rand_r(seed) % 2001 - 1000);
  }

  boost::range::sort(points, compare_points_x);
  boost::range::sort(fixed_x, compare_points_x);
  boost::range::sort(fixed_y, compare_points_x);

  auto min_dist_fast_x = minimal_distance_fast(fixed_x, 0, n - 1);
  auto min_dist_naive_x = minimal_distance_naive(fixed_x, 0, n - 1);

  if (min_dist_fast_x != min_dist_naive_x) {
    std::cout << "Fast: " << min_dist_fast_x << "\n";
    std::cout << "Naive: " << min_dist_naive_x << "\n";

    for (size_t i = 0; i < n; i++) {
      std::cout << fixed_x[i].x << " ";
    }
    std::cout << "\n";
    for (size_t i = 0; i < n; i++) {
      std::cout << fixed_x[i].y << " ";
    }
    std::cout << "\n";
    return false;
  }

  auto min_dist_fast_y = minimal_distance_fast(fixed_y, 0, n - 1);
  auto min_dist_naive_y = minimal_distance_naive(fixed_y, 0, n - 1);

  if (min_dist_fast_y != min_dist_naive_y) {
    std::cout << "Fast: " << min_dist_fast_y << "\n";
    std::cout << "Naive: " << min_dist_naive_y << "\n";

    for (size_t i = 0; i < n; i++) {
      std::cout << fixed_y[i].x << " ";
    }
    std::cout << "\n";
    for (size_t i = 0; i < n; i++) {
      std::cout << fixed_y[i].y << " ";
    }
    std::cout << "\n";
    return false;
  }

  auto min_dist_fast = minimal_distance_fast(points, 0, n - 1);
  auto min_dist_naive = minimal_distance_naive(points, 0, n - 1);

  if (min_dist_fast != min_dist_naive) {
    std::cout << "Fast: " << min_dist_fast << "\n";
    std::cout << "Naive: " << min_dist_naive << "\n";

    for (size_t i = 0; i < n; i++) {
      std::cout << points[i].x << " ";
    }
    std::cout << "\n";
    for (size_t i = 0; i < n; i++) {
      std::cout << points[i].y << " ";
    }
    std::cout << "\n";
    return false;
  }

  return true;
}

int main() {
  int bad = 0;
  int good = 0;
  unsigned int seed = 42;

  for (int i = 0; i < 10000; ++i) {
    auto res = stress_test(&seed);
    if (res) {
      good++;
    } else {
      bad++;
    }
  }

  std::cout << "Bad: " << bad << "\n";
  std::cout << "Good: " << good << "\n";

  // size_t n;
  // std::cin >> n;
  // vector<Point> points(n);
  // for (size_t i = 0; i < n; i++) {
  //   std::cin >> points[i].x >> points[i].y;
  // }
  // std::sort(points.begin(), points.end(), compare_points_x);
  // std::cout << std::fixed;
  // std::cout << std::setprecision(9) << minimal_distance_fast(points, 0, n -
  //                                                            1)
  //           << "\n";
}
